using Aero.Cms.Core.Modules;
using System.Reflection;
using Aero.Cms.Core.Tests.TestModules;

namespace Aero.Cms.Core.Tests.Services;

/// <summary>
/// Service responsible for discovering modules from assemblies.
/// </summary>
public interface IModuleDiscoveryService
{
    /// <summary>
    /// Discovers all valid module descriptors from the specified assemblies.
    /// </summary>
    Task<IReadOnlyList<ModuleDescriptor>> DiscoverAsync(IEnumerable<Assembly>? assemblies = null, CancellationToken ct = default);

    /// <summary>
    /// Discovers all valid module descriptors from the specified types.
    /// </summary>
    Task<IReadOnlyList<ModuleDescriptor>> DiscoverFromTypesAsync(IEnumerable<Type> moduleTypes, CancellationToken ct = default);
}

/// <summary>
/// Default implementation of module discovery.
/// </summary>
public class ModuleDiscoveryService : IModuleDiscoveryService
{
    public Task<IReadOnlyList<ModuleDescriptor>> DiscoverAsync(IEnumerable<Assembly>? assemblies = null, CancellationToken ct = default)
    {
        assemblies ??= AppDomain.CurrentDomain.GetAssemblies();
        var descriptors = new List<ModuleDescriptor>();
        var discoveredNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var assembly in assemblies)
        {
            try
            {
                var moduleTypes = assembly.GetTypes()
                    .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericType: false })
                    .Where(t => typeof(IAeroModule).IsAssignableFrom(t))
                    .Where(t => !t.IsDefined(typeof(ExcludeFromAssemblyDiscoveryAttribute), inherit: false))
                    .ToList();

                foreach (var moduleType in moduleTypes)
                {
                    try
                    {
                        // Try to instantiate to get metadata
                        if (Activator.CreateInstance(moduleType) is not IAeroModule instance)
                            continue;

                        // Check for duplicate names
                        if (!discoveredNames.Add(instance.Name))
                        {
                            throw new InvalidOperationException(
                                $"Duplicate module name '{instance.Name}' detected. " +
                                $"Module type '{moduleType.FullName}' conflicts with an already discovered module.");
                        }

                        var descriptor = new ModuleDescriptor
                        {
                            Name = instance.Name,
                            Version = instance.Version,
                            Author = instance.Author,
                            ModuleType = moduleType,
                            Dependencies = instance.Dependencies,
                            AssemblyName = assembly.GetName().Name ?? "Unknown",
                            PhysicalPath = assembly.Location,
                            IsUiModule = typeof(IUiModule).IsAssignableFrom(moduleType)
                        };

                        descriptors.Add(descriptor);
                    }
                    catch (InvalidOperationException) when (!ct.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch
                    {
                        // Skip modules that can't be instantiated
                        continue;
                    }
                }
            }
            catch (ReflectionTypeLoadException)
            {
                // Skip assemblies that can't be loaded
                continue;
            }
        }

        return Task.FromResult<IReadOnlyList<ModuleDescriptor>>(descriptors);
    }

    public Task<IReadOnlyList<ModuleDescriptor>> DiscoverFromTypesAsync(IEnumerable<Type> moduleTypes, CancellationToken ct = default)
    {
        var descriptors = new List<ModuleDescriptor>();
        var discoveredNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var moduleType in moduleTypes)
        {
            ct.ThrowIfCancellationRequested();

            if (moduleType is not { IsClass: true, IsAbstract: false, IsGenericType: false })
            {
                continue;
            }

            if (!typeof(IAeroModule).IsAssignableFrom(moduleType))
            {
                continue;
            }

            if (Activator.CreateInstance(moduleType) is not IAeroModule instance)
            {
                continue;
            }

            if (!discoveredNames.Add(instance.Name))
            {
                throw new InvalidOperationException(
                    $"Duplicate module name '{instance.Name}' detected. Module type '{moduleType.FullName}' conflicts with an already discovered module.");
            }

            descriptors.Add(new ModuleDescriptor
            {
                Name = instance.Name,
                Version = instance.Version,
                Author = instance.Author,
                ModuleType = moduleType,
                Dependencies = instance.Dependencies,
                AssemblyName = moduleType.Assembly.GetName().Name ?? "Unknown",
                PhysicalPath = moduleType.Assembly.Location,
                IsUiModule = typeof(IUiModule).IsAssignableFrom(moduleType),
                Order = instance.Order,
                Category = instance.Category,
                Tags = instance.Tags,
                DisabledInProduction = instance.DisabledInProduction
            });
        }

        return Task.FromResult<IReadOnlyList<ModuleDescriptor>>(descriptors);
    }
}
