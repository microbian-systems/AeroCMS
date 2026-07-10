namespace Aero.Cms.Shared.Pages.Manager.PageEditor.Catalog;

/// <summary>
/// Defines an interface for INeoEditorCatalogProvider.
/// </summary>
public interface INeoEditorCatalogProvider
{
        /// <summary>
    /// GetCatalogItems method.
    /// </summary>
IReadOnlyList<NeoEditorCatalogItem> GetCatalogItems();
        /// <summary>
    /// TryGet method.
    /// </summary>
bool TryGet(string catalogId, out NeoEditorCatalogItem item);
}
