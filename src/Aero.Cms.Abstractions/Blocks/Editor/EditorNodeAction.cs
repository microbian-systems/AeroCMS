namespace Aero.Cms.Abstractions.Blocks.Editor;

/// <summary>
/// Context menu and canvas actions available in the page editor.
/// Not all actions are available for every node — the
/// <see cref="IEditorNodeActionProvider"/> service computes the available set
/// from <see cref="EditorInteractionCapabilities"/> and runtime editor state.
/// </summary>
public enum EditorNodeAction
{
    /// <summary>Open the property editor modal.</summary>
    Edit,
    /// <summary>Create a copy of the selected node.</summary>
    Duplicate,
    /// <summary>Remove the selected node from the canvas.</summary>
    Delete,
    /// <summary>Copy the selected subtree to the clipboard.</summary>
    Copy,
    /// <summary>Paste clipboard content into this node.</summary>
    Paste,
    /// <summary>Move the node up within its parent.</summary>
    MoveUp,
    /// <summary>Move the node down within its parent.</summary>
    MoveDown,
    /// <summary>Save the selected subtree as a Custom component.</summary>
    SaveAsCustom,
    /// <summary>Open the shared media library.</summary>
    MediaSelect
}
