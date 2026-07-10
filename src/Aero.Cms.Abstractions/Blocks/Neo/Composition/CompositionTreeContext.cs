namespace Aero.Cms.Abstractions.Blocks.Neo.Composition;

/// <summary>
/// Tree state needed to validate one proposed placement without scanning the tree in the policy.
/// </summary>
public sealed record CompositionTreeContext(
    IReadOnlySet<string> MovingNodeDescendantIds,
    int ExistingChildrenInDropZone,
    bool MovingNodeAlreadyInTargetDropZone = false)
{
        /// <summary>
    /// Gets or sets the Empty.
    /// </summary>
public static CompositionTreeContext Empty { get; } =
        new(new HashSet<string>(StringComparer.Ordinal), 0);
}
