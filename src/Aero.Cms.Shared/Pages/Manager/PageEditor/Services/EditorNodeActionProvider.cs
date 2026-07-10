using Aero.Cms.Abstractions.Blocks.Editor;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.Services;

/// <summary>
/// Default implementation. Derives available actions from static definition
/// capabilities and runtime context. Editing commands (undo/redo), drag/drop,
/// and inline interaction are dispatched through separate command services.
/// </summary>
public sealed class EditorNodeActionProvider : IEditorNodeActionProvider
{
        /// <summary>
    /// GetAvailableActions method.
    /// </summary>
public IReadOnlyList<EditorNodeAction> GetAvailableActions(
        EditorInteractionCapabilities caps,
        EditorNodeActionContext ctx)
    {
        var actions = new List<EditorNodeAction>();

        if (caps.HasFlag(EditorInteractionCapabilities.Editable))
            actions.Add(EditorNodeAction.Edit);

        if (caps.HasFlag(EditorInteractionCapabilities.Duplicatable))
            actions.Add(EditorNodeAction.Duplicate);

        if (caps.HasFlag(EditorInteractionCapabilities.Deletable))
            actions.Add(EditorNodeAction.Delete);

        if (caps.HasFlag(EditorInteractionCapabilities.Copyable))
            actions.Add(EditorNodeAction.Copy);

        if (ctx.HasClipboardContent &&
            caps.HasFlag(EditorInteractionCapabilities.PasteTarget))
            actions.Add(EditorNodeAction.Paste);

        if (ctx.CanMoveUp)
            actions.Add(EditorNodeAction.MoveUp);

        if (ctx.CanMoveDown)
            actions.Add(EditorNodeAction.MoveDown);

        if (ctx.CanSaveAsCustom)
            actions.Add(EditorNodeAction.SaveAsCustom);

        if (caps.HasFlag(EditorInteractionCapabilities.MediaSelectable))
            actions.Add(EditorNodeAction.MediaSelect);

        return actions;
    }
}
