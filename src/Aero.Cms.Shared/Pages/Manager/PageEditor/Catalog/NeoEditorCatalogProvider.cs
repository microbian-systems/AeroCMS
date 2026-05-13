namespace Aero.Cms.Shared.Pages.Manager.PageEditor.Catalog;

public partial class NeoEditorCatalogProvider : INeoEditorCatalogProvider
{
    private readonly Dictionary<string, NeoEditorCatalogItem> _items;

    /// <summary>
    /// Declared here, implemented by the source generator
    /// (GeneratedNeoEditorCatalog.g.cs) on server builds. When the generator does
    /// not emit (WASM client builds), this partial declaration is silently removed
    /// by the compiler — the catalog starts empty and the editor shell loads without
    /// crashing. Catalog items can be fetched via a future client-side API endpoint.
    /// </summary>
    partial void PopulateGeneratedCatalog(List<NeoEditorCatalogItem> items);

    public NeoEditorCatalogProvider()
    {
        var items = new List<NeoEditorCatalogItem>();
        PopulateGeneratedCatalog(items);

        _items = new Dictionary<string, NeoEditorCatalogItem>();
        foreach (var item in items)
            _items[item.CatalogId] = item;
    }

    public IReadOnlyList<NeoEditorCatalogItem> GetCatalogItems() => _items.Values.ToList();

    public bool TryGet(string catalogId, out NeoEditorCatalogItem item) =>
        _items.TryGetValue(catalogId, out item!);
}
