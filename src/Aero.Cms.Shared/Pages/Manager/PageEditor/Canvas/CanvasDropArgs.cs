namespace Aero.Cms.Shared.Pages.Manager.PageEditor.Canvas;

/// <summary>
/// Event arguments for drop operations within the composition tree.
/// Carries the target parent node ID, optional sibling node ID for
/// insertion ordering, insertion index, optional palette catalog ID, and
/// the target drop-zone identifier exposed by the parent node definition.
/// </summary>
public sealed record CanvasDropArgs(
    string ParentNodeId,
    string? TargetSiblingNodeId,
    int InsertAtIndex,
    string? CatalogId = null,
    string DropZoneId = "default");
