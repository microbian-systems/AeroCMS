using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Abstractions.Blocks.Editor;

/// <summary>
/// Isolates modal edits until the caller explicitly applies them.
/// </summary>
public sealed class NodeEditorSession
{
    private readonly EditorNodeMemento _original;

    public NodeEditorSession(NeoPageNode node, NodeEditorContext context)
    {
        ArgumentNullException.ThrowIfNull(node);
        Context = context ?? throw new ArgumentNullException(nameof(context));
        _original = EditorNodeMemento.Capture(node);
        WorkingNode = _original.Restore();
    }

    public NodeEditorContext Context { get; }

    public NeoPageNode WorkingNode { get; }

    public NeoPageNode Apply() => EditorNodeMemento.Capture(WorkingNode).Restore();

    public NeoPageNode Cancel() => _original.Restore();
}
