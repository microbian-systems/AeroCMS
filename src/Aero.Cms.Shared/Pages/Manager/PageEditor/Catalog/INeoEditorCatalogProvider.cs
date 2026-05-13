namespace Aero.Cms.Shared.Pages.Manager.PageEditor.Catalog;

public interface INeoEditorCatalogProvider
{
    IReadOnlyList<NeoEditorCatalogItem> GetCatalogItems();
    bool TryGet(string catalogId, out NeoEditorCatalogItem item);
}
