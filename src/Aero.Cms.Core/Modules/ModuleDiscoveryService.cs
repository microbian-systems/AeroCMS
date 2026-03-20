using System.Reflection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aero.Cms.Core.Modules;

/// <summary>
/// Default implementation of module discovery using Scrutor-compatible reflection patterns.
/// </summary>
public sealed class ModuleDiscoveryService : IModuleDiscoveryService
{
    private readonly ModuleDiscoveryOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<ModuleDiscoveryService> _logger;

    public ModuleDiscoveryService(
        IOptions<ModuleDiscoveryOptions> options,
        IHostEnvironment environment,
        ILogger<ModuleDiscoveryService> logger)
    {
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<ModuleDescriptor>> DiscoverAsync(CancellationToken ct = default)
    {
        var descriptors = new List<ModuleDescriptor>();
        var seenNames = new Dictionary<string, ModuleDescriptor>(StringComparer.OrdinalIgnoreCase);

        var assembliesToScan = GetAssembliesToScan();

        foreach (var assembly in assembliesToScan)
        {
            ct.ThrowIfCancellationRequested();

            IEnumerable<Type> moduleTypes;
            try
            {
                moduleTypes = ScanAssemblyForModules(assembly);
            }
            catch (ReflectionTypeLoadException ex)
            {
                _logger.LogWarning(ex, "Partial load of assembly '{Assembly}' - some types may be skipped.", assembly.FullName);
                moduleTypes = ex.Types.Where(t => t != null && IsValidModuleType(t))!;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to scan assembly '{Assembly}' for modules.", assembly.FullName);
                continue;
            }

            foreach (var type in moduleTypes)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    var descriptor = CreateDescriptor(type);

                    // Validate unique module names
                    if (seenNames.TryGetValue(descriptor.Name, out var existing))
                    {
                        throw new DuplicateModuleNameException(
                            descriptor.Name,
                            existing.AssemblyName,
                            descriptor.AssemblyName);
                    }

                    // Skip modules disabled in production unless configured otherwise
                    if (IsDisabledInProduction(type) && !_options.IncludeDisabledInProduction && _environment.IsProduction())
                    {
                        _logger.LogInformation("Skipping module '{ModuleName}' - disabled in production.", descriptor.Name);
                        continue;
                    }

                    seenNames[descriptor.Name] = descriptor;
                    descriptors.Add(descriptor);

                    _logger.LogDebug("Discovered module '{ModuleName}' v{Version} from '{Assembly}'.",
                        descriptor.Name, descriptor.Version, descriptor.AssemblyName);
                }
                catch (DuplicateModuleNameException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create descriptor for type '{Type}' in assembly '{Assembly}'.",
                        type.FullName, assembly.FullName);
                }
            }
        }

        _logger.LogInformation("Discovered {Count} modules.", descriptors.Count);
        return Task.FromResult<IReadOnlyList<ModuleDescriptor>>(descriptors);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<ModuleDescriptor>> DiscoverFromTypesAsync(IEnumerable<Type> moduleTypes, CancellationToken ct = default)
    {
        var descriptors = new List<ModuleDescriptor>();
        var seenNames = new Dictionary<string, ModuleDescriptor>(StringComparer.OrdinalIgnoreCase);

        foreach (var type in moduleTypes)
        {
            ct.ThrowIfCancellationRequested();

            if (!IsValidModuleType(type))
            {
                _logger.LogWarning("Type '{Type}' is not a valid module type - must be non-abstract class implementing IAeroModule.", type.FullName);
                continue;
            }

            try
            {
                var descriptor = CreateDescriptor(type);

                if (seenNames.TryGetValue(descriptor.Name, out var existing))
                {
                    throw new DuplicateModuleNameException(
                        descriptor.Name,
                        existing.AssemblyName,
                        descriptor.AssemblyName);
                }

                seenNames[descriptor.Name] = descriptor;
                descriptors.Add(descriptor);
            }
            catch (DuplicateModuleNameException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create descriptor for type '{Type}'.", type.FullName);
            }
        }

        return Task.FromResult<IReadOnlyList<ModuleDescriptor>>(descriptors);
    }

    private IEnumerable<Assembly> GetAssembliesToScan()
    {
        var assemblies = new HashSet<Assembly>();

        if (_options.ScanApplicationDependencies)
        {
            // Get assemblies from application dependencies using Scrutor-compatible approach
            var entryAssembly = Assembly.GetEntryAssembly();
            if (entryAssembly != null)
            {
                assemblies.Add(entryAssembly);
                foreach (var referenced in entryAssembly.GetReferencedAssemblies())
                {
                    try
                    {
                        if (!IsExcludedAssembly(referenced.Name))
                        {
                            assemblies.Add(Assembly.Load(referenced));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Could not load referenced assembly '{Assembly}'.", referenced.Name);
                    }
                }
            }
        }

        // Scan additional paths
        foreach (var path in _options.AdditionalScanPaths)
        {
            if (!Directory.Exists(path))
            {
                _logger.LogWarning("Module scan path does not exist: '{Path}'.", path);
                continue;
            }

            var dlls = Directory.GetFiles(path, "*.dll", SearchOption.TopDirectoryOnly);
            foreach (var dll in dlls)
            {
                try
                {
                    var assemblyName = Path.GetFileNameWithoutExtension(dll);
                    if (IsExcludedAssembly(assemblyName))
                        continue;

                    var assembly = Assembly.LoadFrom(dll);
                    assemblies.Add(assembly);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not load assembly from '{Path}'.", dll);
                }
            }
        }

        return assemblies;
    }

    private bool IsExcludedAssembly(string? assemblyName)
    {
        if (string.IsNullOrEmpty(assemblyName))
            return true;

        foreach (var pattern in _options.ExcludedAssemblyPatterns)
        {
            if (pattern.EndsWith('*'))
            {
                var prefix = pattern.TrimEnd('*');
                if (assemblyName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            else if (string.Equals(assemblyName, pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private IEnumerable<Type> ScanAssemblyForModules(Assembly assembly)
    {
        var types = assembly.GetTypes();
        return types.Where(IsValidModuleType);
    }

    private bool IsValidModuleType(Type type)
    {
        // Must be non-abstract, non-generic class implementing IAeroModule
        if (!type.IsClass || type.IsAbstract || type.IsGenericTypeDefinition)
            return false;

        if (!typeof(IAeroModule).IsAssignableFrom(type))
            return false;

        // Apply custom type filter if provided
        if (_options.TypeFilter != null && !_options.TypeFilter(type))
            return false;

        return true;
    }

    private static bool IsDisabledInProduction(Type type)
    {
        // Check if type has DisabledInProduction property via instance inspection
        // This is a heuristic - in production, modules should self-declare this
        try
        {
            // Try to find a public static property first (for perf)
            var prop = type.GetProperty("DisabledInProduction", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (prop != null && prop.PropertyType == typeof(bool))
            {
                return (bool)(prop.GetValue(null) ?? false);
            }
        }
        catch
        {
            // Ignore reflection errors
        }

        return false;
    }

    private static ModuleDescriptor CreateDescriptor(Type type)
    {
        // Create a temporary instance to extract metadata
        // This follows the pattern used in ModuleExtensions
        IAeroModule? instance;
        try
        {
            instance = (IAeroModule?)Activator.CreateInstance(type);
        }
        catch (MissingMethodException ex)
        {
            throw new InvalidOperationException($"Module type '{type.FullName}' must have a parameterless constructor.", ex);
        }

        if (instance == null)
        {
            throw new InvalidOperationException($"Failed to create instance of module type '{type.FullName}'.");
        }

        var assembly = type.Assembly;
        var isUiModule = typeof(IUiModule).IsAssignableFrom(type);

        return new ModuleDescriptor
        {
            Name = instance.Name,
            Version = instance.Version,
            Author = instance.Author,
            ModuleType = type,
            Dependencies = instance.Dependencies,
            AssemblyName = assembly.GetName().Name ?? "Unknown",
            PhysicalPath = assembly.Location,
            IsUiModule = isUiModule,
            Order = instance.Order,
            Category = instance.Category,
            Tags = instance.Tags,
            DisabledInProduction = instance.DisabledInProduction
        };
    }
}
