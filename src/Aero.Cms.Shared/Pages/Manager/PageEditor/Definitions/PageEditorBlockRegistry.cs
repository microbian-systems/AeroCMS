using Aero.Cms.Abstractions.Blocks.Editor;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.Definitions;

public static class PageEditorBlockRegistry
{
    private static IReadOnlyDictionary<string, PageEditorDefinitionDescriptor> _definitions =
        new Dictionary<string, PageEditorDefinitionDescriptor>(StringComparer.OrdinalIgnoreCase);

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

    public static bool TryGetDescriptor(
        string? catalogId,
        out PageEditorDefinitionDescriptor definition)
    {
        if (string.IsNullOrWhiteSpace(catalogId))
        {
            definition = default!;
            return false;
        }

        return _definitions.TryGetValue(catalogId, out definition!);
    }

    public static IReadOnlyCollection<IPageEditorBlockDefinition> All =>
        _definitions.Values
            .Select(definition => definition.LegacyDefinition)
            .OfType<IPageEditorBlockDefinition>()
            .ToList();

    public static IReadOnlyCollection<PageEditorDefinitionDescriptor> AllDescriptors =>
        _definitions.Values.ToList();
}
