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
    private readonly Func<HtmlPageContent, Result<bool>>? _validateCandidate;

    /// <summary>
    /// Initializes an editor over a page-content tree.
    /// </summary>
    /// <param name="content">The mutable content owned by this editing session.</param>
    /// <param name="contentPolicy">The policy used for immediate containment decisions.</param>
    /// <param name="history">An optional existing caretaker; a new empty caretaker is created when omitted.</param>
    /// <param name="validateCandidate">An optional whole-document validator for insert and move candidates.</param>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    public HtmlTreeEditor(
        HtmlPageContent content,
        IHtmlContentModelPolicy contentPolicy,
        HtmlPageContentHistory? history = null,
        Func<HtmlPageContent, Result<bool>>? validateCandidate = null)
    {
        Content = content ?? throw new ArgumentNullException(nameof(content));
        _contentPolicy = contentPolicy ?? throw new ArgumentNullException(nameof(contentPolicy));
        _validateCandidate = validateCandidate;
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
    /// <param name="parentNodeId">The stable identity of the direct parent.</param>
    /// <param name="child">The disconnected subtree to insert without cloning.</param>
    /// <param name="index">The requested child index; omitted or out-of-range values are clamped.</param>
    /// <returns>The inserted node, or a not-found, conflict, or validation failure without changing content or history.</returns>
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

        var insertionIndex = NormalizeIndex(index, parent.Children.Count);
        if (_validateCandidate is not null)
        {
            var candidate = HtmlTreeOperations.ClonePreservingNodeIds(Content);
            var candidateParent = HtmlTreeOperations.FindById(candidate.Root, parentNodeId)!;
            candidateParent.Children.Insert(insertionIndex, HtmlTreeOperations.ClonePreservingNodeIds(child));
            var validation = _validateCandidate(candidate);
            if (validation is Result<bool>.Failure failure)
            {
                return failure.Error;
            }
        }

        History.CaptureBeforeChange(Content);
        parent.Children.Insert(insertionIndex, child);
        return child;
    }

    /// <summary>
    /// Inserts an ordered collection of disconnected subtrees as one atomic editor mutation.
    /// This is the boundary used by static HTML fragment import so one undo restores the
    /// complete pre-import document.
    /// </summary>
    /// <param name="parentNodeId">The stable identity of the shared direct parent.</param>
    /// <param name="children">The ordered, disconnected subtrees to insert without cloning.</param>
    /// <param name="index">The requested starting index; omitted or out-of-range values are clamped.</param>
    /// <returns>The supplied children, or a not-found, conflict, or validation failure without partial insertion.</returns>
    public Result<IReadOnlyList<HtmlNode>> InsertChildren(
        long parentNodeId,
        IReadOnlyList<HtmlNode> children,
        int? index = null)
    {
        ArgumentNullException.ThrowIfNull(children);

        if (children.Count == 0)
        {
            return AeroError.ValidationError(["The imported HTML fragment does not contain any insertable elements."]);
        }

        var parent = HtmlTreeOperations.FindById(Content.Root, parentNodeId);
        if (parent is null)
        {
            return AeroError.NotFoundError($"The parent node {parentNodeId} was not found.");
        }

        var importedIds = new HashSet<long>();
        foreach (var child in children)
        {
            if (child is null
                || !HtmlTreeOperations.HasUniqueNodeIds(child)
                || HtmlTreeOperations.FindById(Content.Root, child.NodeId) is not null
                || !CollectIds(child, importedIds))
            {
                return AeroError.ConflictError("A page tree node identity may occur only once.");
            }

            var decision = _contentPolicy.CanContain(parent, child);
            if (!decision.IsAllowed)
            {
                return AeroError.ValidationError([decision.Reason ?? "The imported element cannot be placed in this location."]);
            }
        }

        var insertionIndex = NormalizeIndex(index, parent.Children.Count);
        if (_validateCandidate is not null)
        {
            var candidate = HtmlTreeOperations.ClonePreservingNodeIds(Content);
            var candidateParent = HtmlTreeOperations.FindById(candidate.Root, parentNodeId)!;
            candidateParent.Children.InsertRange(insertionIndex, children.Select(HtmlTreeOperations.ClonePreservingNodeIds));
            var validation = _validateCandidate(candidate);
            if (validation is Result<bool>.Failure failure)
            {
                return failure.Error;
            }
        }

        History.CaptureBeforeChange(Content);
        parent.Children.InsertRange(insertionIndex, children);
        return new Result<IReadOnlyList<HtmlNode>>.Ok(children);
    }

    /// <summary>
    /// Inserts a disconnected subtree before, after, or inside a stable target
    /// identity. Palette adapters use this semantic boundary instead of model indexes.
    /// </summary>
    /// <param name="child">The disconnected subtree to insert.</param>
    /// <param name="targetNodeId">The stable target identity.</param>
    /// <param name="placement">The semantic position relative to the target.</param>
    /// <returns>The inserted node, or a failure without changing content or history.</returns>
    public Result<HtmlNode> InsertRelative(
        HtmlNode child,
        long targetNodeId,
        HtmlRelativePlacement placement)
    {
        ArgumentNullException.ThrowIfNull(child);

        var target = HtmlTreeOperations.FindById(Content.Root, targetNodeId);
        if (target is null)
        {
            return AeroError.NotFoundError($"The target node {targetNodeId} was not found.");
        }

        if (placement is HtmlRelativePlacement.Inside)
        {
            return InsertChild(targetNodeId, child);
        }

        var targetParent = HtmlTreeOperations.FindParentById(Content.Root, targetNodeId);
        if (targetParent is null)
        {
            return AeroError.NotAllowedError("A node cannot be inserted beside the page fragment root.");
        }

        var targetIndex = targetParent.Children.FindIndex(node => node.NodeId == targetNodeId);
        var insertionIndex = placement is HtmlRelativePlacement.After
            ? targetIndex + 1
            : targetIndex;
        return InsertChild(targetParent.NodeId, child, insertionIndex);
    }

    /// <summary>
    /// Removes a non-root node and returns the detached subtree.
    /// </summary>
    /// <param name="nodeId">The stable identity to remove.</param>
    /// <returns>The detached subtree, or a not-found/not-allowed failure.</returns>
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
    /// <param name="nodeId">The stable identity to move.</param>
    /// <param name="destinationParentNodeId">The stable identity of the new direct parent.</param>
    /// <param name="destinationIndex">The requested index after source removal; out-of-range values are clamped.</param>
    /// <returns>The moved node, or a failure without changing content or history.</returns>
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

        var destinationCountAfterRemoval = ReferenceEquals(sourceParent, destinationParent)
            ? destinationParent.Children.Count - 1
            : destinationParent.Children.Count;
        var normalizedDestinationIndex = NormalizeIndex(destinationIndex, destinationCountAfterRemoval);
        if (ReferenceEquals(sourceParent, destinationParent)
            && normalizedDestinationIndex == sourceIndex)
        {
            return node;
        }

        if (_validateCandidate is not null)
        {
            var candidate = HtmlTreeOperations.ClonePreservingNodeIds(Content);
            var candidateSourceParent = HtmlTreeOperations.FindById(candidate.Root, sourceParent.NodeId)!;
            var candidateDestinationParent = HtmlTreeOperations.FindById(candidate.Root, destinationParentNodeId)!;
            var candidateNodeIndex = candidateSourceParent.Children.FindIndex(child => child.NodeId == nodeId);
            var candidateNode = candidateSourceParent.Children[candidateNodeIndex];
            candidateSourceParent.Children.RemoveAt(candidateNodeIndex);
            candidateDestinationParent.Children.Insert(normalizedDestinationIndex, candidateNode);
            var validation = _validateCandidate(candidate);
            if (validation is Result<bool>.Failure failure)
            {
                return failure.Error;
            }
        }

        History.CaptureBeforeChange(Content);
        sourceParent.Children.RemoveAt(sourceIndex);
        destinationParent.Children.Insert(normalizedDestinationIndex, node);
        return node;
    }

    /// <summary>
    /// Moves an existing node before, after, or inside another node. The target's
    /// stable identity keeps browser geometry independent from text-node indexes.
    /// </summary>
    /// <param name="nodeId">The stable identity to move.</param>
    /// <param name="targetNodeId">The stable target identity.</param>
    /// <param name="placement">The semantic position relative to the target.</param>
    /// <returns>The moved node, or a failure without changing content or history.</returns>
    public Result<HtmlNode> MoveRelative(
        long nodeId,
        long targetNodeId,
        HtmlRelativePlacement placement)
    {
        if (nodeId == targetNodeId)
        {
            return AeroError.NotAllowedError("A node cannot be moved relative to itself.");
        }

        var target = HtmlTreeOperations.FindById(Content.Root, targetNodeId);
        if (target is null)
        {
            return AeroError.NotFoundError($"The target node {targetNodeId} was not found.");
        }

        if (placement is HtmlRelativePlacement.Inside)
        {
            return Move(nodeId, targetNodeId, target.Children.Count);
        }

        var sourceParent = HtmlTreeOperations.FindParentById(Content.Root, nodeId);
        if (sourceParent is null)
        {
            return Content.Root.NodeId == nodeId
                ? AeroError.NotAllowedError("The page fragment root cannot be moved.")
                : AeroError.NotFoundError($"The node {nodeId} was not found.");
        }

        var targetParent = HtmlTreeOperations.FindParentById(Content.Root, targetNodeId);
        if (targetParent is null)
        {
            return AeroError.NotAllowedError("A node cannot be placed beside the page fragment root.");
        }

        var sourceIndex = sourceParent.Children.FindIndex(child => child.NodeId == nodeId);
        var targetIndex = targetParent.Children.FindIndex(child => child.NodeId == targetNodeId);
        if (ReferenceEquals(sourceParent, targetParent) && sourceIndex < targetIndex)
        {
            targetIndex--;
        }

        var destinationIndex = placement is HtmlRelativePlacement.After
            ? targetIndex + 1
            : targetIndex;
        return Move(nodeId, targetParent.NodeId, destinationIndex);
    }

    /// <summary>
    /// Atomically replaces the editable properties of one element. The candidate
    /// document is committed only after the supplied validation strategy succeeds.
    /// </summary>
    /// <param name="nodeId">The stable element identity to update.</param>
    /// <param name="properties">The replacement attribute, class, style, and optional literal-text state.</param>
    /// <param name="validateCandidate">The authoritative whole-document validation strategy.</param>
    /// <returns>The updated node in the newly committed tree, or a failure preserving the current tree and history.</returns>
    public Result<HtmlNode> UpdateProperties(
        long nodeId,
        HtmlNodeProperties properties,
        Func<HtmlPageContent, Result<bool>> validateCandidate)
    {
        ArgumentNullException.ThrowIfNull(properties);
        ArgumentNullException.ThrowIfNull(validateCandidate);

        var existing = HtmlTreeOperations.FindById(Content.Root, nodeId);
        if (existing is null)
        {
            return AeroError.NotFoundError($"The node {nodeId} was not found.");
        }

        if (existing.Kind is not HtmlNodeKind.Element)
        {
            return AeroError.NotAllowedError("Only HTML element properties can be edited.");
        }

        var candidateContent = HtmlTreeOperations.ClonePreservingNodeIds(Content);
        var candidateNode = HtmlTreeOperations.FindById(candidateContent.Root, nodeId)!;
        candidateNode.Attributes = new Dictionary<string, string>(
            properties.Attributes,
            StringComparer.Ordinal);
        candidateNode.ThemeClasses = [.. properties.ThemeClasses];
        candidateNode.Style = HtmlTreeOperations.CloneStyle(properties.Style);
        if (properties.ReplaceChildrenWithLiteralText)
        {
            candidateNode.Children = string.IsNullOrEmpty(properties.LiteralText)
                ? []
                : [HtmlNode.CreateText(properties.LiteralText)];
        }

        var validation = validateCandidate(candidateContent);
        if (validation is Result<bool>.Failure failure)
        {
            return failure.Error;
        }

        History.CaptureBeforeChange(Content);
        Content = candidateContent;
        return candidateNode;
    }

    /// <summary>
    /// Atomically replaces the ordered children of one element. Rich-text and
    /// other child editors use this boundary so candidate validation occurs
    /// before the current document or its Memento history changes.
    /// </summary>
    /// <param name="nodeId">The stable element identity to update.</param>
    /// <param name="children">Source child subtrees; the committed copies receive fresh identities.</param>
    /// <param name="validateCandidate">The authoritative whole-document validation strategy.</param>
    /// <returns>The updated node in the newly committed tree, or a failure preserving the current tree and history.</returns>
    public Result<HtmlNode> UpdateChildren(
        long nodeId,
        IReadOnlyList<HtmlNode> children,
        Func<HtmlPageContent, Result<bool>> validateCandidate)
    {
        ArgumentNullException.ThrowIfNull(children);
        ArgumentNullException.ThrowIfNull(validateCandidate);

        var existing = HtmlTreeOperations.FindById(Content.Root, nodeId);
        if (existing is null)
        {
            return AeroError.NotFoundError($"The node {nodeId} was not found.");
        }

        if (existing.Kind is not HtmlNodeKind.Element)
        {
            return AeroError.NotAllowedError("Only HTML element children can be edited.");
        }

        var candidateContent = HtmlTreeOperations.ClonePreservingNodeIds(Content);
        var candidateNode = HtmlTreeOperations.FindById(candidateContent.Root, nodeId)!;
        candidateNode.Children = children
            .Select(HtmlTreeOperations.CloneWithFreshNodeIds)
            .ToList();

        var validation = validateCandidate(candidateContent);
        if (validation is Result<bool>.Failure failure)
        {
            return failure.Error;
        }

        History.CaptureBeforeChange(Content);
        Content = candidateContent;
        return candidateNode;
    }

    /// <summary>
    /// Applies one guided structural mutation to a cloned subtree and commits it
    /// as one Memento only after the complete candidate document is valid.
    /// </summary>
    /// <param name="nodeId">The stable element identity to update.</param>
    /// <param name="updateCandidate">The mutation applied only to the isolated candidate node.</param>
    /// <param name="validateCandidate">The authoritative whole-document validation strategy.</param>
    /// <returns>The updated node in the newly committed tree, or a failure preserving the current tree and history.</returns>
    public Result<HtmlNode> UpdateStructure(
        long nodeId,
        Action<HtmlNode> updateCandidate,
        Func<HtmlPageContent, Result<bool>> validateCandidate)
    {
        ArgumentNullException.ThrowIfNull(updateCandidate);
        ArgumentNullException.ThrowIfNull(validateCandidate);

        var existing = HtmlTreeOperations.FindById(Content.Root, nodeId);
        if (existing is null)
        {
            return AeroError.NotFoundError($"The node {nodeId} was not found.");
        }

        if (existing.Kind is not HtmlNodeKind.Element)
        {
            return AeroError.NotAllowedError("Only HTML element structure can be edited.");
        }

        var candidateContent = HtmlTreeOperations.ClonePreservingNodeIds(Content);
        var candidateNode = HtmlTreeOperations.FindById(candidateContent.Root, nodeId)!;
        updateCandidate(candidateNode);

        var validation = validateCandidate(candidateContent);
        if (validation is Result<bool>.Failure failure)
        {
            return failure.Error;
        }

        History.CaptureBeforeChange(Content);
        Content = candidateContent;
        return candidateNode;
    }

    /// <summary>
    /// Restores the preceding content snapshot, if available.
    /// </summary>
    /// <returns>The restored tree, or a not-allowed failure when no undo snapshot exists.</returns>
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
    /// <returns>The restored tree, or a not-allowed failure when no redo snapshot exists.</returns>
    public Result<HtmlPageContent> Redo()
    {
        var result = History.Redo(Content);
        if (result is Result<HtmlPageContent>.Ok restored)
        {
            Content = restored.Value;
        }

        return result;
    }

    /// <summary>Clamps a requested insertion index and treats omission as append.</summary>
    private static int NormalizeIndex(int? requestedIndex, int collectionCount) =>
        Math.Clamp(requestedIndex ?? collectionCount, 0, collectionCount);

    /// <summary>Collects a subtree's identities into shared batch state and stops at the first duplicate.</summary>
    private static bool CollectIds(HtmlNode node, ISet<long> ids)
    {
        if (!ids.Add(node.NodeId))
        {
            return false;
        }

        return node.Children.All(child => CollectIds(child, ids));
    }
}
