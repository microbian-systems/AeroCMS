namespace Aero.Cms.Html;

/// <summary>
/// Resource limits applied before an imported fragment becomes a page-content tree.
/// </summary>
public sealed record HtmlFragmentImportLimits
{
    /// <summary>
    /// Maximum source characters accepted for one import.
    /// </summary>
    public int MaximumSourceLength { get; init; } = 1_000_000;

    /// <summary>
    /// Maximum element nesting depth. This intentionally matches the content-validator default.
    /// </summary>
    public int MaximumDepth { get; init; } = 64;

    /// <summary>
    /// Maximum imported node count. This intentionally matches the content-validator default.
    /// </summary>
    public int MaximumNodeCount { get; init; } = 5_000;
}
