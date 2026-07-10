using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Abstractions.Blocks.Editor;

/// <summary>
/// Isolates modal edits until the caller explicitly applies them.
/// </summary>
public sealed class NodeEditorSession
{
    private readonly EditorNodeMemento _original;

        /// <summary>
    /// Initializes a new instance of the <see cref="NodeEditorSession"/> class.
    /// </summary>
public NodeEditorSession(NeoPageNode node, NodeEditorContext context)
    {
        ArgumentNullException.ThrowIfNull(node);
        Context = context ?? throw new ArgumentNullException(nameof(context));
        _original = EditorNodeMemento.Capture(node);
        WorkingNode = _original.Restore();
    }

        /// <summary>
    /// Gets or sets the Context.
    /// </summary>
public NodeEditorContext Context { get; }

        /// <summary>
    /// Gets or sets the Working Node.
    /// </summary>
public NeoPageNode WorkingNode { get; }

        /// <summary>
    /// Apply method.
    /// </summary>
public NeoPageNode Apply() => EditorNodeMemento.Capture(WorkingNode).Restore();

        /// <summary>
    /// Cancel method.
    /// </summary>
public NeoPageNode Cancel() => _original.Restore();
}
