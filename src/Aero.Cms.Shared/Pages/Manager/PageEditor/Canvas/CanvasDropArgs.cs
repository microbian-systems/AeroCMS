namespace Aero.Cms.Shared.Pages.Manager.PageEditor.Canvas;

/// <summary>
/// Event arguments for drop operations within the composition tree.
/// Carries the target parent node ID, optional sibling node ID for
/// insertion ordering, and the insertion index.
/// </summary>
public sealed record CanvasDropArgs(
    string ParentNodeId,
    string? TargetSiblingNodeId,
    int InsertAtIndex,
    string? CatalogId = null);
