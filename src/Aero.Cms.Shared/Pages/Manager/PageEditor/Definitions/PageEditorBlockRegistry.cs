using Aero.Cms.Abstractions.Blocks.Editor;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.Definitions;

public static class PageEditorBlockRegistry
{
    private static IReadOnlyDictionary<string, IPageEditorBlockDefinition> _definitions =
        new Dictionary<string, IPageEditorBlockDefinition>(StringComparer.OrdinalIgnoreCase);

    public static void RegisterProviders(IEnumerable<IPageEditorBlockProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        _definitions = _definitions.Values
            .Concat(providers.SelectMany(provider => provider.GetDefinitions()))
            .GroupBy(definition => definition.CatalogId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
    }

    public static bool TryGet(string? catalogId, out IPageEditorBlockDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(catalogId))
        {
            definition = default!;
            return false;
        }

        return _definitions.TryGetValue(catalogId, out definition!);
    }

    public static IReadOnlyCollection<IPageEditorBlockDefinition> All => _definitions.Values.ToList();
}
