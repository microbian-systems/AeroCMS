using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Html;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

/// <summary>
/// Stores one immutable editor snapshot so HTML and typed-content composition
/// always cross the undo/redo boundary together.
/// </summary>
internal sealed class PageEditorDocumentMemento
{
    private readonly HtmlPageContent _content;
    private readonly PageCompositionDocument _composition;

    private PageEditorDocumentMemento(
        HtmlPageContent content,
        PageCompositionDocument composition)
    {
        _content = content;
        _composition = composition;
    }

    public static PageEditorDocumentMemento Capture(
        HtmlPageContent content,
        PageCompositionDocument composition)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(composition);

        return new PageEditorDocumentMemento(
            HtmlTreeOperations.ClonePreservingNodeIds(content),
            composition.CreateSnapshot());
    }

    public PageEditorDocumentState Restore() => new(
        HtmlTreeOperations.ClonePreservingNodeIds(_content),
        _composition.CreateSnapshot());
}

/// <summary>
/// Restored aggregate state for one page-editor document.
/// </summary>
internal sealed record PageEditorDocumentState(
    HtmlPageContent Content,
    PageCompositionDocument Composition);

/// <summary>
/// Memento caretaker for the aggregate page-editor document.
/// </summary>
internal sealed class PageEditorDocumentHistory
{
    private readonly Stack<PageEditorDocumentMemento> _undo = new();
    private readonly Stack<PageEditorDocumentMemento> _redo = new();

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public void CaptureBeforeChange(PageEditorDocumentMemento memento)
    {
        ArgumentNullException.ThrowIfNull(memento);
        _undo.Push(memento);
        _redo.Clear();
    }

    public Result<PageEditorDocumentState> Undo(
        HtmlPageContent currentContent,
        PageCompositionDocument currentComposition)
    {
        ArgumentNullException.ThrowIfNull(currentContent);
        ArgumentNullException.ThrowIfNull(currentComposition);

        if (_undo.Count == 0)
        {
            return AeroError.NotAllowedError("There is no page-editor change to undo.");
        }

        _redo.Push(PageEditorDocumentMemento.Capture(currentContent, currentComposition));
        return _undo.Pop().Restore();
    }

    public Result<PageEditorDocumentState> Redo(
        HtmlPageContent currentContent,
        PageCompositionDocument currentComposition)
    {
        ArgumentNullException.ThrowIfNull(currentContent);
        ArgumentNullException.ThrowIfNull(currentComposition);

        if (_redo.Count == 0)
        {
            return AeroError.NotAllowedError("There is no page-editor change to redo.");
        }

        _undo.Push(PageEditorDocumentMemento.Capture(currentContent, currentComposition));
        return _redo.Pop().Restore();
    }
}
