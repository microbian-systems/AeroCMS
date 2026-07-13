namespace Aero.Cms.Html;

/// <summary>
/// Immutable snapshot of a page's editable HTML tree for the Memento undo/redo pattern.
/// </summary>
public sealed class HtmlPageContentMemento
{
    private readonly HtmlPageContent _snapshot;

    private HtmlPageContentMemento(HtmlPageContent snapshot)
    {
        _snapshot = snapshot;
    }

    /// <summary>
    /// Captures an independent snapshot of the supplied content.
    /// </summary>
    public static HtmlPageContentMemento Capture(HtmlPageContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return new HtmlPageContentMemento(HtmlTreeOperations.ClonePreservingNodeIds(content));
    }

    /// <summary>
    /// Restores an independent copy so future edits cannot mutate this snapshot.
    /// </summary>
    public HtmlPageContent Restore() => HtmlTreeOperations.ClonePreservingNodeIds(_snapshot);
}
