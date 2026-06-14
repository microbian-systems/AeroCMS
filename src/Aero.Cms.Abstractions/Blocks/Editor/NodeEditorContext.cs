using Aero.Cms.Abstractions.Blocks.Neo.Styles;

namespace Aero.Cms.Abstractions.Blocks.Editor;

/// <summary>
/// Immutable context supplied to a node property editor.
/// </summary>
public sealed record NodeEditorContext(
    EditorBreakpoint Breakpoint,
    string Culture,
    ContentDirection Direction);
