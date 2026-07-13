using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Html;

/// <summary>
/// Caretaker for content-only Memento snapshots used by the page editor.
/// Selection and transient UI state deliberately remain outside this history.
/// </summary>
public sealed class HtmlPageContentHistory
{
    private readonly Stack<HtmlPageContentMemento> _undo = new();
    private readonly Stack<HtmlPageContentMemento> _redo = new();

    /// <summary>
    /// Gets whether an undo snapshot is available.
    /// </summary>
    public bool CanUndo => _undo.Count > 0;

    /// <summary>
    /// Gets whether a redo snapshot is available.
    /// </summary>
    public bool CanRedo => _redo.Count > 0;

    /// <summary>
    /// Records the current content before an editor command changes it.
    /// A new edit invalidates the redo branch.
    /// </summary>
    public void CaptureBeforeChange(HtmlPageContent content)
    {
        _undo.Push(HtmlPageContentMemento.Capture(content));
        _redo.Clear();
    }

    /// <summary>
    /// Restores the prior snapshot and records the supplied current state for redo.
    /// </summary>
    public Result<HtmlPageContent> Undo(HtmlPageContent current)
    {
        ArgumentNullException.ThrowIfNull(current);

        if (_undo.Count == 0)
        {
            return AeroError.NotAllowedError("There is no page-content change to undo.");
        }

        _redo.Push(HtmlPageContentMemento.Capture(current));
        return _undo.Pop().Restore();
    }

    /// <summary>
    /// Restores the next snapshot and records the supplied current state for undo.
    /// </summary>
    public Result<HtmlPageContent> Redo(HtmlPageContent current)
    {
        ArgumentNullException.ThrowIfNull(current);

        if (_redo.Count == 0)
        {
            return AeroError.NotAllowedError("There is no page-content change to redo.");
        }

        _undo.Push(HtmlPageContentMemento.Capture(current));
        return _redo.Pop().Restore();
    }

    /// <summary>
    /// Clears all stored snapshots, such as after loading another page.
    /// </summary>
    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}
