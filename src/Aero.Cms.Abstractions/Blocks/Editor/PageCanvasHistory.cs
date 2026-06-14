namespace Aero.Cms.Abstractions.Blocks.Editor;

public sealed class PageCanvasHistory
{
    private readonly int _capacity;
    private readonly List<HistoryEntry> _undo = [];
    private readonly List<HistoryEntry> _redo = [];
    private EditorBlockListMemento _current;

    public PageCanvasHistory(IReadOnlyList<EditorBlock> initialState, int capacity = 100)
    {
        ArgumentNullException.ThrowIfNull(initialState);
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);

        _capacity = capacity;
        _current = EditorBlockListMemento.Capture(initialState);
    }

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public void Record(IReadOnlyList<EditorBlock> nextState)
    {
        ArgumentNullException.ThrowIfNull(nextState);

        var next = EditorBlockListMemento.Capture(nextState);
        _undo.Add(new HistoryEntry(_current, next));
        if (_undo.Count > _capacity)
        {
            _undo.RemoveRange(0, _undo.Count - _capacity);
        }

        _redo.Clear();
        _current = next;
    }

    public List<EditorBlock> Undo()
    {
        if (!CanUndo)
        {
            return _current.Restore();
        }

        var entry = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _redo.Add(entry);
        _current = entry.Before;
        return _current.Restore();
    }

    public List<EditorBlock> Redo()
    {
        if (!CanRedo)
        {
            return _current.Restore();
        }

        var entry = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        _undo.Add(entry);
        _current = entry.After;
        return _current.Restore();
    }

    private sealed record HistoryEntry(
        EditorBlockListMemento Before,
        EditorBlockListMemento After);
}
