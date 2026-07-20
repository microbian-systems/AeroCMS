namespace Aero.Cms.Html;

/// <summary>
/// Public rendering output. The host decides where the validated CSS is placed.
/// </summary>
public sealed class RenderedHtmlPage
{
    /// <summary>Gets validated markup whose text and attribute values have been HTML encoded.</summary>
    public required string Markup { get; init; }
    /// <summary>Gets the separately hosted stylesheet associated with <see cref="Markup"/>.</summary>
    public required string CssText { get; init; }
    /// <summary>Gets the compiled style hash that hosts may use for cache keys or asset identity.</summary>
    public required string StyleContentHash { get; init; }
}
