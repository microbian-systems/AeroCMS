namespace Aero.Cms.Html;

/// <summary>
/// Result of a page-level style compilation pass.
/// </summary>
public sealed class CompiledPageStyles
{
    /// <summary>Gets the framework or generated class names assigned to each stable node identity.</summary>
    public required IReadOnlyDictionary<long, IReadOnlyList<string>> NodeClasses { get; init; }

    /// <summary>Gets the deterministic stylesheet for style intent that was not represented by framework classes.</summary>
    public required string CssText { get; init; }

    /// <summary>Gets the deterministic lowercase SHA-256 identity produced by the selected compiler.</summary>
    public required string ContentHash { get; init; }

    /// <summary>Gets the identifier of the style profile used by the compilation pass.</summary>
    public required string ProfileId { get; init; }

    /// <summary>Gets the version of the style profile used by the compilation pass.</summary>
    public required string ProfileVersion { get; init; }

    /// <summary>
    /// Gets the compiled classes for a node without exposing the dictionary lookup to renderers.
    /// </summary>
    /// <param name="nodeId">The stable editor identity of the node.</param>
    /// <returns>The node's classes, or an empty collection when no classes were compiled for it.</returns>
    public IReadOnlyList<string> ClassesFor(long nodeId) =>
        NodeClasses.TryGetValue(nodeId, out var classes) ? classes : [];
}
