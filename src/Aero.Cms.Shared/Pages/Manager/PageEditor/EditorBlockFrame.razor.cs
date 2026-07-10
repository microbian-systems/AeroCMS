using Aero.Cms.Abstractions.Blocks.Editor;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor;

/// <summary>
/// Represents a class for EditorBlockFrame.
/// </summary>
public sealed partial class EditorBlockFrame : ComponentBase
{
        /// <summary>
    /// Gets or sets the Block Editor Id.
    /// </summary>
[Parameter] public string BlockEditorId { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
[Parameter] public string BlockType { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Index.
    /// </summary>
[Parameter] public int Index { get; set; }
        /// <summary>
    /// Gets or sets the Total Count.
    /// </summary>
[Parameter] public int TotalCount { get; set; }
        /// <summary>
    /// Gets or sets the Is Selected.
    /// </summary>
[Parameter] public bool IsSelected { get; set; }
        /// <summary>
    /// Gets or sets the Dragging.
    /// </summary>
[Parameter] public bool Dragging { get; set; }
        /// <summary>
    /// Gets or sets the Is Drag Over.
    /// </summary>
[Parameter] public bool IsDragOver { get; set; }

        /// <summary>
    /// Gets or sets the Toolbar Actions.
    /// </summary>
[Parameter] public RenderFragment? ToolbarActions { get; set; }
        /// <summary>
    /// Gets or sets the Child Content.
    /// </summary>
[Parameter] public RenderFragment? ChildContent { get; set; }

        /// <summary>
    /// Gets or sets the On Select.
    /// </summary>
[Parameter] public EventCallback OnSelect { get; set; }
        /// <summary>
    /// Gets or sets the On Open Editor.
    /// </summary>
[Parameter] public EventCallback OnOpenEditor { get; set; }
        /// <summary>
    /// Gets or sets the On Move Up.
    /// </summary>
[Parameter] public EventCallback<int> OnMoveUp { get; set; }
        /// <summary>
    /// Gets or sets the On Move Down.
    /// </summary>
[Parameter] public EventCallback<int> OnMoveDown { get; set; }
        /// <summary>
    /// Gets or sets the On Duplicate.
    /// </summary>
[Parameter] public EventCallback<int> OnDuplicate { get; set; }
        /// <summary>
    /// Gets or sets the On Delete.
    /// </summary>
[Parameter] public EventCallback<int> OnDelete { get; set; }
        /// <summary>
    /// Gets or sets the On Copy.
    /// </summary>
[Parameter] public EventCallback<int> OnCopy { get; set; }
        /// <summary>
    /// Gets or sets the On Paste.
    /// </summary>
[Parameter] public EventCallback<int> OnPaste { get; set; }
        /// <summary>
    /// Gets or sets the On Save As Custom.
    /// </summary>
[Parameter] public EventCallback<int> OnSaveAsCustom { get; set; }
        /// <summary>
    /// Gets or sets the On Drag Start.
    /// </summary>
[Parameter] public EventCallback<DragStartEventArgs> OnDragStart { get; set; }
        /// <summary>
    /// Gets or sets the On Drag Over.
    /// </summary>
[Parameter] public EventCallback<int> OnDragOver { get; set; }
        /// <summary>
    /// Gets or sets the On Drop.
    /// </summary>
[Parameter] public EventCallback<int> OnDrop { get; set; }

    [Inject] private IPageEditorDefinitionRegistry DefinitionRegistry { get; set; } = default!;

    private bool ContextMenuOpen { get; set; }
    private double ContextMenuX { get; set; }
    private double ContextMenuY { get; set; }
    private IReadOnlyList<EditorNodeAction> _contextMenuActions = Array.Empty<EditorNodeAction>();

    private async Task OpenContextMenu(MouseEventArgs args)
    {
        ContextMenuX = args.ClientX;
        ContextMenuY = args.ClientY;
        _contextMenuActions = ComputeAvailableActions();
        ContextMenuOpen = true;
        await OnSelect.InvokeAsync();
    }

    private IReadOnlyList<EditorNodeAction> ComputeAvailableActions()
    {
        if (!DefinitionRegistry.TryGetDescriptor(BlockType, out var descriptor))
            return Array.Empty<EditorNodeAction>();

        var interaction = descriptor.Interaction;
        var context = new EditorNodeActionContext(
            HasClipboardContent: false,
            CanMoveUp: Index > 0,
            CanMoveDown: Index < TotalCount - 1,
            CanSaveAsCustom: interaction.HasFlag(EditorInteractionCapabilities.Editable));

        return ActionProvider.GetAvailableActions(interaction, context);
    }

    private async Task ExecuteContextMenuAction(EditorNodeAction action)
    {
        ContextMenuOpen = false;

        switch (action)
        {
            case EditorNodeAction.Edit:
                await OnOpenEditor.InvokeAsync();
                break;
            case EditorNodeAction.Duplicate:
                if (OnDuplicate.HasDelegate)
                    await OnDuplicate.InvokeAsync(Index);
                break;
            case EditorNodeAction.Delete:
                if (OnDelete.HasDelegate)
                    await OnDelete.InvokeAsync(Index);
                break;
            case EditorNodeAction.Copy:
                if (OnCopy.HasDelegate)
                    await OnCopy.InvokeAsync(Index);
                break;
            case EditorNodeAction.Paste:
                if (OnPaste.HasDelegate)
                    await OnPaste.InvokeAsync(Index);
                break;
            case EditorNodeAction.MoveUp:
                if (OnMoveUp.HasDelegate)
                    await OnMoveUp.InvokeAsync(Index);
                break;
            case EditorNodeAction.MoveDown:
                if (OnMoveDown.HasDelegate)
                    await OnMoveDown.InvokeAsync(Index);
                break;
            case EditorNodeAction.SaveAsCustom:
                if (OnSaveAsCustom.HasDelegate)
                    await OnSaveAsCustom.InvokeAsync(Index);
                break;
            // MediaSelect is intentionally skipped — no handler yet
        }
    }

    private string GetActionDisplayText(EditorNodeAction action) => action switch
    {
        EditorNodeAction.Edit => L["Edit"],
        EditorNodeAction.Delete => L["Delete"],
        EditorNodeAction.Duplicate => L["Duplicate"],
        EditorNodeAction.Copy => L["Copy"],
        EditorNodeAction.Paste => L["Paste"],
        EditorNodeAction.MoveUp => L["MoveUp"],
        EditorNodeAction.MoveDown => L["MoveDown"],
        EditorNodeAction.SaveAsCustom => L["SaveAsCustom"],
        EditorNodeAction.MediaSelect => L["MediaSelect"],
        _ => action.ToString()
    };
}

/// <summary>
/// Represents a class for DragStartEventArgs.
/// </summary>
public sealed class DragStartEventArgs
{
        /// <summary>
    /// Gets or sets the Editor Id.
    /// </summary>
public string EditorId { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Index.
    /// </summary>
public int Index { get; set; }
}
