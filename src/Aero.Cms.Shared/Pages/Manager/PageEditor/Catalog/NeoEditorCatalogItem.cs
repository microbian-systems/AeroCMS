namespace Aero.Cms.Shared.Pages.Manager.PageEditor.Catalog;

/// <summary>
/// Represents a record for NeoEditorCatalogItem.
/// </summary>
public sealed record NeoEditorCatalogItem
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public required string CatalogId { get; init; }
        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public required string DisplayName { get; init; }
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description { get; init; }
        /// <summary>
    /// Gets or sets the Section.
    /// </summary>
public required NeoEditorCatalogSection Section { get; init; }
        /// <summary>
    /// Gets or sets the Kind.
    /// </summary>
public required NeoEditorCatalogKind Kind { get; init; }
        /// <summary>
    /// Gets or sets the Icon Name.
    /// </summary>
public string IconName { get; init; } = "box";
        /// <summary>
    /// Gets or sets the Sort Order.
    /// </summary>
public int SortOrder { get; init; }
        /// <summary>
    /// Gets or sets the Allow Children.
    /// </summary>
public bool AllowChildren { get; init; }
        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe { get; init; } = true;
        /// <summary>
    /// Gets or sets the Requires Interactive Island.
    /// </summary>
public bool RequiresInteractiveIsland { get; init; }
        /// <summary>
    /// Gets or sets the Editor Preview Component Type.
    /// </summary>
public Type? EditorPreviewComponentType { get; init; }
        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType { get; init; }
        /// <summary>
    /// Gets or sets the Public Renderer Component Type.
    /// </summary>
public Type? PublicRendererComponentType { get; init; }
        /// <summary>
    /// Gets or sets the Property Definitions.
    /// </summary>
public IReadOnlyList<NeoPropertyDefinition> PropertyDefinitions { get; init; } = [];
        /// <summary>
    /// Gets or sets the Allowed Child Catalog Ids.
    /// </summary>
public IReadOnlySet<string> AllowedChildCatalogIds { get; init; } = new HashSet<string>();
        /// <summary>
    /// Gets or sets the Allowed Parent Catalog Ids.
    /// </summary>
public IReadOnlySet<string> AllowedParentCatalogIds { get; init; } = new HashSet<string>();
}
