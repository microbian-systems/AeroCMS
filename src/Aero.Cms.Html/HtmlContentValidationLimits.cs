namespace Aero.Cms.Html;

/// <summary>
/// Resource limits that keep recursive page content safe to validate and render.
/// </summary>
public sealed record HtmlContentValidationLimits
{
    /// <summary>Gets the maximum root-relative depth accepted by recursive validation.</summary>
    public int MaximumDepth { get; init; } = 64;

    /// <summary>Gets the maximum number of nodes accepted in one page-content tree.</summary>
    public int MaximumNodeCount { get; init; } = 5_000;
}
