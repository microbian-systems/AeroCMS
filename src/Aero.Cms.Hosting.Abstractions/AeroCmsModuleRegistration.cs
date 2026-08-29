using System.Reflection;
using Aero.Modular;
using Orleans.Hosting;
using Wolverine;

namespace Aero.Cms.Hosting;

/// <summary>
/// Describes the compile-time registrations contributed by one selected Aero CMS package.
/// </summary>
public sealed class AeroCmsModuleRegistration
{
    /// <summary>
    /// Initializes a module registration.
    /// </summary>
    /// <param name="id">Stable package registration identifier.</param>
    /// <param name="moduleDescriptors">Source-generated module descriptors.</param>
    /// <param name="configureWolverine">Deterministic Wolverine handler registration.</param>
    /// <param name="configureOrleans">Deterministic Orleans grain and serializer registration.</param>
    /// <param name="serverComponentAssemblies">Explicit server component assemblies.</param>
    /// <param name="webAssemblyComponentAssemblies">Explicit WebAssembly component assemblies.</param>
    /// <param name="orleansApplicationPartAssemblies">Assemblies containing source-generated Orleans grain application parts.</param>
    /// <param name="capabilities">Capabilities supplied by the registration.</param>
    public AeroCmsModuleRegistration(
        string id,
        IReadOnlyList<ModuleDescriptor> moduleDescriptors,
        Action<WolverineOptions> configureWolverine,
        Action<ISiloBuilder> configureOrleans,
        IReadOnlyList<Assembly>? serverComponentAssemblies = null,
        IReadOnlyList<Assembly>? webAssemblyComponentAssemblies = null,
        IReadOnlyList<Assembly>? orleansApplicationPartAssemblies = null,
        AeroCmsCapabilities capabilities = AeroCmsCapabilities.None)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(moduleDescriptors);
        ArgumentNullException.ThrowIfNull(configureWolverine);
        ArgumentNullException.ThrowIfNull(configureOrleans);
        if (moduleDescriptors.Any(static descriptor => descriptor is null))
        {
            throw new ArgumentException(
                "Module descriptors cannot contain null entries.",
                nameof(moduleDescriptors));
        }

        Id = id.Trim();
        ModuleDescriptors = moduleDescriptors.ToArray();
        ConfigureWolverine = configureWolverine;
        ConfigureOrleans = configureOrleans;
        ServerComponentAssemblies = CopyAssemblies(serverComponentAssemblies);
        WebAssemblyComponentAssemblies = CopyAssemblies(webAssemblyComponentAssemblies);
        OrleansApplicationPartAssemblies = CopyAssemblies(orleansApplicationPartAssemblies);
        Capabilities = capabilities;
    }

    /// <summary>Gets the stable registration identifier.</summary>
    public string Id { get; }

    /// <summary>Gets the module descriptors supplied by this package.</summary>
    public IReadOnlyList<ModuleDescriptor> ModuleDescriptors { get; }

    /// <summary>Gets the deterministic Wolverine registration callback.</summary>
    public Action<WolverineOptions> ConfigureWolverine { get; }

    /// <summary>Gets the deterministic Orleans registration callback.</summary>
    public Action<ISiloBuilder> ConfigureOrleans { get; }

    /// <summary>Gets explicitly selected server component assemblies.</summary>
    public IReadOnlyList<Assembly> ServerComponentAssemblies { get; }

    /// <summary>Gets explicitly selected WebAssembly component assemblies.</summary>
    public IReadOnlyList<Assembly> WebAssemblyComponentAssemblies { get; }

    /// <summary>
    /// Gets explicitly selected assemblies whose Orleans source-generated application-part metadata
    /// must be loaded into the silo.
    /// </summary>
    public IReadOnlyList<Assembly> OrleansApplicationPartAssemblies { get; }

    /// <summary>Gets the capabilities supplied by this package.</summary>
    public AeroCmsCapabilities Capabilities { get; }

    private static IReadOnlyList<Assembly> CopyAssemblies(IReadOnlyList<Assembly>? assemblies)
        => assemblies is null ? [] : assemblies.ToArray();
}
