using Aero.Cms.Abstractions.Blocks.Editor;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.Definitions;

/// <summary>
/// Represents a class for PageEditorBlockRegistry.
/// </summary>
[Obsolete("Use IPageEditorDefinitionRegistry via DI instead. This class is a deprecated compatibility shim.")]
public static class PageEditorBlockRegistry
{
    private static IReadOnlyDictionary<string, PageEditorDefinitionDescriptor> _definitions =
        new Dictionary<string, PageEditorDefinitionDescriptor>(StringComparer.OrdinalIgnoreCase);
    private static IPageEditorDefinitionRegistry? _activeRegistry;

    /// <summary>
    /// Installs the DI-backed registry used by legacy static callers during
    /// the Phase 0.5 migration. New code should inject
    /// <see cref="IPageEditorDefinitionRegistry"/> directly.
    /// </summary>
    public static void UseRegistry(IPageEditorDefinitionRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _activeRegistry = registry;
    }

        /// <summary>
    /// RegisterProviders method.
    /// </summary>
[Obsolete("Use IPageEditorDefinitionRegistry from DI instead.")]
    public static void RegisterProviders(
        IEnumerable<IPageEditorBlockProvider> providers,
        IEnumerable<IPageEditorDefinitionProvider>? nativeProviders = null)
    {
        ArgumentNullException.ThrowIfNull(providers);

        var legacyDefinitions = providers
            .SelectMany(provider => provider.GetDefinitions())
            .Select(definition => new LegacyPageEditorDefinitionAdapter(definition).ToDescriptor());
        var nativeDefinitions = nativeProviders?
            .SelectMany(provider => provider.GetEditorDefinitions()) ?? [];

        _definitions = _definitions.Values
            .Concat(legacyDefinitions)
            .Concat(nativeDefinitions)
            .GroupBy(definition => definition.CatalogId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
    }

        /// <summary>
    /// TryGet method.
    /// </summary>
public static bool TryGet(string? catalogId, out IPageEditorBlockDefinition definition)
    {
        if (TryGetDescriptor(catalogId, out var descriptor) &&
            descriptor.LegacyDefinition is { } legacyDefinition)
        {
            definition = legacyDefinition;
            return true;
        }

        definition = default!;
        return false;
    }

        /// <summary>
    /// TryGetDescriptor method.
    /// </summary>
public static bool TryGetDescriptor(
        string? catalogId,
        out PageEditorDefinitionDescriptor definition)
    {
        if (_activeRegistry is not null)
        {
            return _activeRegistry.TryGetDescriptor(catalogId, out definition);
        }

        if (string.IsNullOrWhiteSpace(catalogId))
        {
            definition = default!;
            return false;
        }

        return _definitions.TryGetValue(catalogId, out definition!);
    }

        /// <summary>
    /// Gets or sets the All.
    /// </summary>
public static IReadOnlyCollection<IPageEditorBlockDefinition> All =>
        _activeRegistry?.LegacyDefinitions ??
        _definitions.Values
            .Select(definition => definition.LegacyDefinition)
            .OfType<IPageEditorBlockDefinition>()
            .ToList();

        /// <summary>
    /// Gets or sets the All Descriptors.
    /// </summary>
public static IReadOnlyCollection<PageEditorDefinitionDescriptor> AllDescriptors =>
        _activeRegistry?.AllDescriptors ?? _definitions.Values.ToList();
}
