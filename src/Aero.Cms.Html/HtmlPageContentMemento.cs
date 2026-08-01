namespace Aero.Cms.Html;

/// <summary>
/// Immutable snapshot of a page's editable HTML tree for the Memento undo/redo pattern.
/// </summary>
public sealed class HtmlPageContentMemento
{
    private readonly HtmlPageContent _snapshot;

    /// <summary>Stores an already independent snapshot for later clone-on-restore operations.</summary>
    private HtmlPageContentMemento(HtmlPageContent snapshot)
    {
        _snapshot = snapshot;
    }

    /// <summary>
    /// Captures an independent snapshot of the supplied content.
    /// </summary>
    /// <param name="content">The current content state.</param>
    /// <returns>A snapshot isolated from subsequent changes to <paramref name="content"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> is <see langword="null"/>.</exception>
    public static HtmlPageContentMemento Capture(HtmlPageContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return new HtmlPageContentMemento(HtmlTreeOperations.ClonePreservingNodeIds(content));
    }

    /// <summary>
    /// Restores an independent copy so future edits cannot mutate this snapshot.
    /// </summary>
    /// <returns>A new page tree preserving the captured node identities.</returns>
    public HtmlPageContent Restore() => HtmlTreeOperations.ClonePreservingNodeIds(_snapshot);
}
