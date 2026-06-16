namespace Aero.Cms.Abstractions.Blocks.Editor;

/// <summary>
/// Converts a node's <see cref="EditorInteractionCapabilities"/> and runtime
/// <see cref="EditorNodeActionContext"/> into the set of currently available
/// context menu and canvas actions.
///
/// Final architecture. Registered as a scoped service. Consumers (Razor
/// components like EditorBlockFrame, SortableCompositionSurface,
/// PageEditorCanvas) call <see cref="GetAvailableActions"/> to build
/// capability-aware context menus, replacing the current hardcoded switches.
/// </summary>
public interface IEditorNodeActionProvider
{
    /// <summary>
    /// Returns the actions available for a node given its definition
    /// capabilities and the current editor session state.
    /// </summary>
    IReadOnlyList<EditorNodeAction> GetAvailableActions(
        EditorInteractionCapabilities capabilities,
        EditorNodeActionContext context);
}
