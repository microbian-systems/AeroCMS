namespace Aero.Cms.Abstractions.Blocks.Editor;

/// <summary>
/// Declares which canvas interactions are available for a node definition.
/// Intentionally separate from <see cref="EditorCapabilitySet"/>, which
/// describes property editor groups (Content, Typography, Background, etc.).
///
/// Final architecture. Part of the Command + Strategy pattern:
/// concrete definitions declare what is possible; the
/// <c>IEditorNodeActionProvider</c> service consumes these flags plus editor
/// session state to return the currently available context menu actions.
///
/// Expected usage: every concrete catalog definition declares its interaction
/// flags explicitly. Primitive leaf nodes typically declare Selectable,
/// Editable, Draggable, Duplicatable, Deletable, Copyable.
/// containers add PasteTarget. Image primitives add MediaSelectable.
/// </summary>
[Flags]
public enum EditorInteractionCapabilities
{
    None = 0,
    Selectable = 1 << 0,
    Editable = 1 << 1,
    Draggable = 1 << 2,
    Duplicatable = 1 << 3,
    Deletable = 1 << 4,
    Copyable = 1 << 5,
    PasteTarget = 1 << 6,
    SaveAsCustom = 1 << 7,
    MediaSelectable = 1 << 8
}
