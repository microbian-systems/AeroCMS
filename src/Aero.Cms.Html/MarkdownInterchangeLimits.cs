namespace Aero.Cms.Html;

/// <summary>
/// Resource limits for one Markdown interchange operation.
/// </summary>
public sealed record MarkdownInterchangeLimits
{
    /// <summary>
    /// Maximum Markdown characters accepted for one import.
    /// </summary>
    public int MaximumMarkdownLength { get; init; } = 1_000_000;

    /// <summary>
    /// Maximum intermediate HTML characters accepted before policy validation.
    /// </summary>
    public int MaximumGeneratedHtmlLength { get; init; } = 1_000_000;

    /// <summary>
    /// Maximum Markdown characters produced by one export.
    /// </summary>
    public int MaximumExportLength { get; init; } = 1_000_000;
}
