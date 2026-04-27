using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Abstractions.Blocks.Common;

/// <summary>
/// A block that allows injecting raw HTML, CSS, or JavaScript directly into the page.
/// </summary>
[BlockMetadata("raw_html", "Raw HTML", Category = "Advanced")]
public sealed class RawHtmlBlock : BlockBase
{
    public override string BlockType => "raw_html";

    /// <summary>
    /// Gets or sets the raw HTML content.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}

/// <summary>
/// A block for specialized analytics and tracking pixel injection on a per-page basis.
/// </summary>
[BlockMetadata("analytics_script", "Analytics Script", Category = "Marketing")]
public sealed class AnalyticsBlock : BlockBase
{
    public override string BlockType => "analytics_script";

    /// <summary>
    /// Gets or sets the provider name (e.g., "google_ads", "hotjar").
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the ID or token for the provider.
    /// </summary>
    public string TrackingId { get; set; } = string.Empty;

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
