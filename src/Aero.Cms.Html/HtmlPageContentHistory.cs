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
    /// <param name="content">The state that undo must restore.</param>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> is <see langword="null"/>.</exception>
    public void CaptureBeforeChange(HtmlPageContent content)
    {
        _undo.Push(HtmlPageContentMemento.Capture(content));
        _redo.Clear();
    }

    /// <summary>
    /// Restores the prior snapshot and records the supplied current state for redo.
    /// </summary>
    /// <param name="current">The state to capture on the redo stack before restoration.</param>
    /// <returns>The restored independent tree, or a not-allowed failure when no undo snapshot exists.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="current"/> is <see langword="null"/>.</exception>
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
    /// <param name="current">The state to capture on the undo stack before restoration.</param>
    /// <returns>The restored independent tree, or a not-allowed failure when no redo snapshot exists.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="current"/> is <see langword="null"/>.</exception>
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
