using Aero.Modular;

namespace Aero.Cms.Web.Generated;

/// <summary>
/// Stable host module catalog API. Source generation populates the provider list when available.
/// </summary>
public static partial class GeneratedAeroModuleCatalog
{
    public static IReadOnlyList<IModuleManifestProvider> Providers { get; } = CreateProviders();

    public static IReadOnlyList<ModuleDescriptor> Descriptors { get; } =
        Providers.SelectMany(provider => provider.GetDescriptors()).ToArray();

    private static IReadOnlyList<IModuleManifestProvider> CreateProviders()
    {
        var providers = new List<IModuleManifestProvider>();
        PopulateProviders(providers);
        return providers;
    }

    static partial void PopulateProviders(List<IModuleManifestProvider> providers);
}
