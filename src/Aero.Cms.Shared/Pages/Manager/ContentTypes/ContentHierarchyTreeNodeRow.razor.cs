using Aero.Cms.Abstractions.Http.Clients;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.ContentTypes;

/// <summary>Renders one recursive, accessible manager hierarchy node.</summary>
public partial class ContentHierarchyTreeNodeRow
{
    [Parameter, EditorRequired]
    public ContentHierarchyTreeNode Node { get; set; } = default!;

    [Parameter, EditorRequired]
    public IReadOnlySet<long> ExpandedIds { get; set; } = new HashSet<long>();

    [Parameter]
    public long? SelectedId { get; set; }

    [Parameter]
    public bool Dragging { get; set; }

    [Parameter]
    public EventCallback<ContentHierarchyTreeNode> Selected { get; set; }

    [Parameter]
    public EventCallback<long> ToggleRequested { get; set; }

    [Parameter]
    public EventCallback<ContentHierarchyMoveIntent> MoveCommandRequested { get; set; }

    [Parameter]
    public EventCallback<long> DragStarted { get; set; }

    [Parameter]
    public EventCallback DragEnded { get; set; }

    [Parameter]
    public EventCallback<ContentHierarchyDropIntent> DropRequested { get; set; }

    private bool IsExpanded => ExpandedIds.Contains(Node.Id);

    private bool IsSelected => SelectedId == Node.Id;

    private string RowClass => IsSelected
        ? "aero-content-tree-node__row aero-content-tree-node__row--selected"
        : "aero-content-tree-node__row";

    private string StatusClass => string.Equals(
        Node.PublicationState,
        "Published",
        StringComparison.OrdinalIgnoreCase)
            ? "aero-content-tree-node__status aero-content-tree-node__status--published"
            : "aero-content-tree-node__status";

    private Task SelectAsync() => Selected.InvokeAsync(Node);

    private Task ToggleAsync() => ToggleRequested.InvokeAsync(Node.Id);

    private Task CommandAsync(ContentHierarchyMoveCommand command)
        => MoveCommandRequested.InvokeAsync(new(Node.Id, command));

    private Task StartDragAsync()
        => DragStarted.InvokeAsync(Node.Id);

    private Task EndDragAsync()
        => DragEnded.InvokeAsync();

    private Task DropAsync(ContentHierarchyDropPlacement placement)
        => DropRequested.InvokeAsync(new(Node.Id, placement));
}

/// <summary>Keyboard/button-accessible hierarchy movement commands.</summary>
public enum ContentHierarchyMoveCommand
{
    Up,
    Down,
    IntoPrevious,
    Out,
    Root
}

/// <summary>One explicit move command emitted by a hierarchy node.</summary>
public sealed record ContentHierarchyMoveIntent(long ItemId, ContentHierarchyMoveCommand Command);

/// <summary>Pointer drop placement relative to a target node.</summary>
public enum ContentHierarchyDropPlacement
{
    Before,
    Inside,
    After
}

/// <summary>One pointer drop intent emitted by a target hierarchy node.</summary>
public sealed record ContentHierarchyDropIntent(long TargetId, ContentHierarchyDropPlacement Placement);
