namespace Aero.Cms.Shared.Pages.Manager.PageEditor.Catalog;

public static class NeoCatalogSectionMapper
{
    public static bool TryMap(string? category, out NeoEditorCatalogSection section)
    {
        switch (category?.Trim().ToUpperInvariant())
        {
            case "AERO UI":
                section = NeoEditorCatalogSection.AeroUi;
                return true;
            case "PRIMITIVES":
                section = NeoEditorCatalogSection.Primitives;
                return true;
            case "COMPONENTS":
                section = NeoEditorCatalogSection.Components;
                return true;
            default:
                section = default;
                return false;
        }
    }
}
