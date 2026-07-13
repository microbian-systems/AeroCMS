namespace Aero.Cms.Html;

/// <summary>
/// Result of a page-level style compilation pass.
/// </summary>
public sealed class CompiledPageStyles
{
    public required IReadOnlyDictionary<long, IReadOnlyList<string>> NodeClasses { get; init; }
    public required string CssText { get; init; }
    public required string ContentHash { get; init; }
    public required string ProfileId { get; init; }
    public required string ProfileVersion { get; init; }

    public IReadOnlyList<string> ClassesFor(long nodeId) =>
        NodeClasses.TryGetValue(nodeId, out var classes) ? classes : [];
}
