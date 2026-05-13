using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor;

public sealed partial class EditorBlockFrame : ComponentBase
{
    [Parameter] public string BlockEditorId { get; set; } = string.Empty;
    [Parameter] public int Index { get; set; }
    [Parameter] public int TotalCount { get; set; }
    [Parameter] public bool IsSelected { get; set; }
    [Parameter] public bool Dragging { get; set; }
    [Parameter] public bool IsDragOver { get; set; }

    [Parameter] public EventCallback OnSelect { get; set; }
    [Parameter] public EventCallback<int> OnMoveUp { get; set; }
    [Parameter] public EventCallback<int> OnMoveDown { get; set; }
    [Parameter] public EventCallback<int> OnDuplicate { get; set; }
    [Parameter] public EventCallback<int> OnDelete { get; set; }
    [Parameter] public EventCallback<DragStartEventArgs> OnDragStart { get; set; }
    [Parameter] public EventCallback<int> OnDragOver { get; set; }
    [Parameter] public EventCallback<int> OnDrop { get; set; }

    [Parameter] public RenderFragment? ChildContent { get; set; }
}

public sealed class DragStartEventArgs
{
    public string EditorId { get; set; } = string.Empty;
    public int Index { get; set; }
}
