using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Html;

/// <summary>
/// Applies editor tree mutations and owns their Memento history.
/// Browser drag mechanics call this state-transition boundary; they do not mutate the document directly.
/// </summary>
public sealed class HtmlTreeEditor
{
    private readonly IHtmlContentModelPolicy _contentPolicy;

    /// <summary>
    /// Initializes an editor over a page-content tree.
    /// </summary>
    public HtmlTreeEditor(
        HtmlPageContent content,
        IHtmlContentModelPolicy contentPolicy,
        HtmlPageContentHistory? history = null)
    {
        Content = content ?? throw new ArgumentNullException(nameof(content));
        _contentPolicy = contentPolicy ?? throw new ArgumentNullException(nameof(contentPolicy));
        History = history ?? new HtmlPageContentHistory();
    }

    /// <summary>
    /// Gets the current editable content tree.
    /// </summary>
    public HtmlPageContent Content { get; private set; }

    /// <summary>
    /// Gets the Memento caretaker for this editing session.
    /// </summary>
    public HtmlPageContentHistory History { get; }

    /// <summary>
    /// Inserts a disconnected, identity-unique subtree into a valid parent.
    /// </summary>
    public Result<HtmlNode> InsertChild(long parentNodeId, HtmlNode child, int? index = null)
    {
        ArgumentNullException.ThrowIfNull(child);

        var parent = HtmlTreeOperations.FindById(Content.Root, parentNodeId);
        if (parent is null)
        {
            return AeroError.NotFoundError($"The parent node {parentNodeId} was not found.");
        }

        if (!HtmlTreeOperations.HasUniqueNodeIds(child)
            || HtmlTreeOperations.FindById(Content.Root, child.NodeId) is not null)
        {
            return AeroError.ConflictError("A page tree node identity may occur only once.");
        }

        var decision = _contentPolicy.CanContain(parent, child);
        if (!decision.IsAllowed)
        {
            return AeroError.ValidationError([decision.Reason ?? "The child cannot be placed in this element."]);
        }

        History.CaptureBeforeChange(Content);
        parent.Children.Insert(NormalizeIndex(index, parent.Children.Count), child);
        return child;
    }

    /// <summary>
    /// Removes a non-root node and returns the detached subtree.
    /// </summary>
    public Result<HtmlNode> Remove(long nodeId)
    {
        if (Content.Root.NodeId == nodeId)
        {
            return AeroError.NotAllowedError("The page fragment root cannot be removed.");
        }

        var parent = HtmlTreeOperations.FindParentById(Content.Root, nodeId);
        if (parent is null)
        {
            return AeroError.NotFoundError($"The node {nodeId} was not found.");
        }

        var index = parent.Children.FindIndex(child => child.NodeId == nodeId);
        var node = parent.Children[index];
        History.CaptureBeforeChange(Content);
        parent.Children.RemoveAt(index);
        return node;
    }

    /// <summary>
    /// Moves an existing node to a destination position interpreted after its removal from the source parent.
    /// </summary>
    public Result<HtmlNode> Move(long nodeId, long destinationParentNodeId, int destinationIndex)
    {
        if (Content.Root.NodeId == nodeId)
        {
            return AeroError.NotAllowedError("The page fragment root cannot be moved.");
        }

        var sourceParent = HtmlTreeOperations.FindParentById(Content.Root, nodeId);
        var destinationParent = HtmlTreeOperations.FindById(Content.Root, destinationParentNodeId);
        if (sourceParent is null)
        {
            return AeroError.NotFoundError($"The node {nodeId} was not found.");
        }

        if (destinationParent is null)
        {
            return AeroError.NotFoundError($"The destination parent {destinationParentNodeId} was not found.");
        }

        var sourceIndex = sourceParent.Children.FindIndex(child => child.NodeId == nodeId);
        var node = sourceParent.Children[sourceIndex];
        if (HtmlTreeOperations.FindById(node, destinationParentNodeId) is not null)
        {
            return AeroError.NotAllowedError("A node cannot be moved into itself or one of its descendants.");
        }

        var decision = _contentPolicy.CanContain(destinationParent, node);
        if (!decision.IsAllowed)
        {
            return AeroError.ValidationError([decision.Reason ?? "The node cannot be moved to this element."]);
        }

        History.CaptureBeforeChange(Content);
        sourceParent.Children.RemoveAt(sourceIndex);
        destinationParent.Children.Insert(NormalizeIndex(destinationIndex, destinationParent.Children.Count), node);
        return node;
    }

    /// <summary>
    /// Restores the preceding content snapshot, if available.
    /// </summary>
    public Result<HtmlPageContent> Undo()
    {
        var result = History.Undo(Content);
        if (result is Result<HtmlPageContent>.Ok restored)
        {
            Content = restored.Value;
        }

        return result;
    }

    /// <summary>
    /// Restores the next content snapshot, if available.
    /// </summary>
    public Result<HtmlPageContent> Redo()
    {
        var result = History.Redo(Content);
        if (result is Result<HtmlPageContent>.Ok restored)
        {
            Content = restored.Value;
        }

        return result;
    }

    private static int NormalizeIndex(int? requestedIndex, int collectionCount) =>
        Math.Clamp(requestedIndex ?? collectionCount, 0, collectionCount);
}
