namespace Aero.Cms.Shared.Pages.Manager.PageEditor.Catalog;

public sealed record NeoEditorCatalogItem
{
    public required string CatalogId { get; init; }
    public required string DisplayName { get; init; }
    public string? Description { get; init; }
    public required NeoEditorCatalogSection Section { get; init; }
    public required NeoEditorCatalogKind Kind { get; init; }
    public string IconName { get; init; } = "box";
    public int SortOrder { get; init; }
    public bool AllowChildren { get; init; }
    public bool PublicStaticSsrSafe { get; init; } = true;
    public bool RequiresInteractiveIsland { get; init; }
    public Type? EditorPreviewComponentType { get; init; }
    public Type? PropertyEditorComponentType { get; init; }
    public Type? PublicRendererComponentType { get; init; }
    public IReadOnlyList<NeoPropertyDefinition> PropertyDefinitions { get; init; } = [];
    public IReadOnlySet<string> AllowedChildCatalogIds { get; init; } = new HashSet<string>();
    public IReadOnlySet<string> AllowedParentCatalogIds { get; init; } = new HashSet<string>();
}
