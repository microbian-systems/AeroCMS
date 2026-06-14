using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Abstractions.Blocks.Editor;

/// <summary>
/// Bounded Memento history for a composition root. Consecutive edits with the
/// same coalescing key replace the latest entry, which keeps typing and resize
/// gestures from creating an undo step for every event.
/// </summary>
public sealed class CompositionHistory
{
    private readonly int _capacity;
    private readonly List<HistoryEntry> _undo = [];
    private readonly List<HistoryEntry> _redo = [];

    public CompositionHistory(NeoPageNode initialState, int capacity = 100)
    {
        ArgumentNullException.ThrowIfNull(initialState);
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);

        _capacity = capacity;
        Current = EditorNodeMemento.Capture(initialState).Restore();
    }

    public NeoPageNode Current { get; private set; }

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public void Record(NeoPageNode nextState, string? coalescingKey = null)
    {
        ArgumentNullException.ThrowIfNull(nextState);

        var before = EditorNodeMemento.Capture(Current);
        var after = EditorNodeMemento.Capture(nextState);

        if (!string.IsNullOrWhiteSpace(coalescingKey) &&
            _undo.Count > 0 &&
            string.Equals(
                _undo[^1].CoalescingKey,
                coalescingKey,
                StringComparison.Ordinal))
        {
            _undo[^1] = _undo[^1] with { After = after };
        }
        else
        {
            _undo.Add(new HistoryEntry(before, after, coalescingKey));
            TrimToCapacity();
        }

        _redo.Clear();
        Current = after.Restore();
    }

    public NeoPageNode Undo()
    {
        if (!CanUndo)
        {
            return EditorNodeMemento.Capture(Current).Restore();
        }

        var entry = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _redo.Add(entry);
        Current = entry.Before.Restore();
        return EditorNodeMemento.Capture(Current).Restore();
    }

    public NeoPageNode Redo()
    {
        if (!CanRedo)
        {
            return EditorNodeMemento.Capture(Current).Restore();
        }

        var entry = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        _undo.Add(entry);
        Current = entry.After.Restore();
        return EditorNodeMemento.Capture(Current).Restore();
    }

    private void TrimToCapacity()
    {
        if (_undo.Count > _capacity)
        {
            _undo.RemoveRange(0, _undo.Count - _capacity);
        }
    }

    private sealed record HistoryEntry(
        EditorNodeMemento Before,
        EditorNodeMemento After,
        string? CoalescingKey);
}
