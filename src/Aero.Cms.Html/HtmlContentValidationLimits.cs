namespace Aero.Cms.Html;

/// <summary>
/// Resource limits that keep recursive page content safe to validate and render.
/// </summary>
public sealed record HtmlContentValidationLimits
{
    public int MaximumDepth { get; init; } = 64;
    public int MaximumNodeCount { get; init; } = 5_000;
}
