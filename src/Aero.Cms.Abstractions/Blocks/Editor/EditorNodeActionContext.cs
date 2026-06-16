namespace Aero.Cms.Abstractions.Blocks.Editor;

/// <summary>
/// Runtime editor state needed to compute available actions.
/// Supplements <see cref="EditorInteractionCapabilities"/> with transient
/// session state that cannot be encoded in a static definition.
/// </summary>
public sealed record EditorNodeActionContext(
    bool HasClipboardContent,
    bool CanMoveUp,
    bool CanMoveDown,
    bool CanSaveAsCustom);
