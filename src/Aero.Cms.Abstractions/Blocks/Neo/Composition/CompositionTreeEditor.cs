using Aero.Cms.Abstractions.Blocks.Editor;

namespace Aero.Cms.Abstractions.Blocks.Neo.Composition;

/// <summary>
/// Applies immutable, policy-checked mutations to a Neo composition tree.
/// </summary>
public sealed class CompositionTreeEditor(ICompositionPolicy policy) : ICompositionTreeEditor
{
        /// <summary>
    /// Drop method.
    /// </summary>
public Result<IReadOnlyList<NeoPageNode>, AeroError> Drop(
        IReadOnlyList<NeoPageNode> roots,
        CompositionDropRequest request)
    {
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(request);

        var workingRoots = CloneRoots(roots);
        var movingNode = Find(workingRoots, request.Node.NodeId);
        var node = movingNode?.Node ?? EditorNodeMemento.Capture(request.Node).Restore();
        var parent = string.IsNullOrWhiteSpace(request.ParentNodeId)
            ? null
            : Find(workingRoots, request.ParentNodeId)?.Node;

        if (!string.IsNullOrWhiteSpace(request.ParentNodeId) && parent is null)
        {
            return Invalid($"Parent node '{request.ParentNodeId}' was not found.");
        }

        var targetChildren = parent?.Children ?? workingRoots;
        var context = new CompositionTreeContext(
            DescendantIds(node),
            targetChildren.Count,
            movingNode is not null &&
            ReferenceEquals(movingNode.Parent, parent));
        var validation = policy.ValidatePlacement(
            node,
            parent,
            request.DropZoneId,
            context);

        if (validation is Result<bool, AeroError>.Failure failure)
        {
            return failure.Error;
        }

        if (movingNode is not null)
        {
            movingNode.Siblings.RemoveAt(movingNode.Index);
        }

        targetChildren = parent?.Children ?? workingRoots;
        var targetIndex = Math.Clamp(request.TargetIndex, 0, targetChildren.Count);
        targetChildren.Insert(targetIndex, node);

        return workingRoots;
    }

        /// <summary>
    /// Remove method.
    /// </summary>
public Result<IReadOnlyList<NeoPageNode>, AeroError> Remove(
        IReadOnlyList<NeoPageNode> roots,
        string nodeId)
    {
        ArgumentNullException.ThrowIfNull(roots);

        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return Invalid("A node ID is required.");
        }

        var workingRoots = CloneRoots(roots);
        var match = Find(workingRoots, nodeId);
        if (match is null)
        {
            return Invalid($"Node '{nodeId}' was not found.");
        }

        match.Siblings.RemoveAt(match.Index);
        return workingRoots;
    }

    private static List<NeoPageNode> CloneRoots(IReadOnlyList<NeoPageNode> roots) =>
        roots.Select(node => EditorNodeMemento.Capture(node).Restore()).ToList();

    private static NodeLocation? Find(
        List<NeoPageNode> siblings,
        string nodeId,
        NeoPageNode? parent = null)
    {
        for (var index = 0; index < siblings.Count; index++)
        {
            var node = siblings[index];
            if (string.Equals(node.NodeId, nodeId, StringComparison.Ordinal))
            {
                return new NodeLocation(node, parent, siblings, index);
            }

            var child = Find(node.Children, nodeId, node);
            if (child is not null)
            {
                return child;
            }
        }

        return null;
    }

    private static IReadOnlySet<string> DescendantIds(NeoPageNode node)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        AddDescendants(node, ids);
        return ids;
    }

    private static void AddDescendants(NeoPageNode node, ISet<string> ids)
    {
        foreach (var child in node.Children)
        {
            ids.Add(child.NodeId);
            AddDescendants(child, ids);
        }
    }

    private static Result<IReadOnlyList<NeoPageNode>, AeroError> Invalid(string message) =>
        AeroError.ValidationError([message]);

    private sealed record NodeLocation(
        NeoPageNode Node,
        NeoPageNode? Parent,
        List<NeoPageNode> Siblings,
        int Index);
}
