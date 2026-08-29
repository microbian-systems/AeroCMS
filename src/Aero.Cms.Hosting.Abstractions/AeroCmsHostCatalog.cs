using System.Reflection;
using Aero.Modular;
using Orleans.Hosting;
using Orleans.Serialization;
using Wolverine;

namespace Aero.Cms.Hosting;

/// <summary>
/// Immutable, validated hosting catalog consumed by C# and F# ASP.NET Core hosts.
/// </summary>
public sealed class AeroCmsHostCatalog
{
    internal AeroCmsHostCatalog(
        IReadOnlyList<AeroCmsModuleRegistration> registrations,
        IReadOnlyList<ModuleDescriptor> moduleDescriptors,
        IReadOnlyList<Assembly> serverComponentAssemblies,
        IReadOnlyList<Assembly> webAssemblyComponentAssemblies,
        AeroCmsCapabilities capabilities)
    {
        Registrations = registrations;
        ModuleDescriptors = moduleDescriptors;
        ServerComponentAssemblies = serverComponentAssemblies;
        WebAssemblyComponentAssemblies = webAssemblyComponentAssemblies;
        Capabilities = capabilities;
    }

    /// <summary>Gets registrations in deterministic dependency-first order.</summary>
    public IReadOnlyList<AeroCmsModuleRegistration> Registrations { get; }

    /// <summary>Gets module descriptors in deterministic dependency-first order.</summary>
    public IReadOnlyList<ModuleDescriptor> ModuleDescriptors { get; }

    /// <summary>Gets distinct server component assemblies.</summary>
    public IReadOnlyList<Assembly> ServerComponentAssemblies { get; }

    /// <summary>Gets distinct WebAssembly component assemblies.</summary>
    public IReadOnlyList<Assembly> WebAssemblyComponentAssemblies { get; }

    /// <summary>Gets all capabilities supplied by the selected registrations.</summary>
    public AeroCmsCapabilities Capabilities { get; }

    /// <summary>
    /// Applies Wolverine callbacks in deterministic registration order.
    /// </summary>
    /// <param name="options">Wolverine options to configure.</param>
    public void ConfigureWolverine(WolverineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        foreach (var registration in Registrations)
        {
            registration.ConfigureWolverine(options);
        }
    }

    /// <summary>
    /// Applies Orleans callbacks in deterministic registration order.
    /// </summary>
    /// <param name="builder">Orleans silo builder to configure.</param>
    public void ConfigureOrleans(ISiloBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var applicationParts = Registrations
            .SelectMany(static registration => registration.OrleansApplicationPartAssemblies)
            .DistinctBy(static assembly => assembly.FullName, StringComparer.Ordinal)
            .ToArray();
        builder.Services.AddSerializer(serializer =>
        {
            foreach (var applicationPart in applicationParts)
            {
                // Orleans 7+ discovers grains from build-time generated application-part metadata.
                // Explicitly registering every selected grain assembly loads that metadata without
                // falling back to base-directory or AppDomain scanning.
                serializer.AddAssembly(applicationPart);
            }
        });

        foreach (var registration in Registrations)
        {
            registration.ConfigureOrleans(builder);
        }
    }

    /// <summary>
    /// Fails when the catalog does not provide all required capabilities.
    /// </summary>
    /// <param name="required">Capabilities required by enabled host options.</param>
    public void RequireCapabilities(AeroCmsCapabilities required)
    {
        var missing = required & ~Capabilities;
        if (missing != AeroCmsCapabilities.None)
        {
            throw new AeroCmsCatalogException(
                "AEROCMS_CATALOG_MISSING_CAPABILITY",
                $"The selected Aero CMS catalog is missing required capabilities: {missing}.");
        }
    }
}
