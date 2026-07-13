namespace Aero.Cms.Html;

/// <summary>
/// Public rendering output. The host decides where the validated CSS is placed.
/// </summary>
public sealed class RenderedHtmlPage
{
    public required string Markup { get; init; }
    public required string CssText { get; init; }
    public required string StyleContentHash { get; init; }
}
