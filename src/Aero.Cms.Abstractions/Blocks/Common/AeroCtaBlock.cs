using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Abstractions.Blocks.Common;

/// <summary>
/// A Call-To-Action (CTA) block for driving user conversions.
/// </summary>
[BlockMetadata("aero_cta", "Aero CTA", Category = "Aero")]
public class AeroCtaBlock : BlockBase
{
    public override string BlockType => "aero_cta";

    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? CtaText { get; set; }
    public string? CtaUrl { get; set; }
    public string? ImageUrl { get; set; }
    public AeroCtaLayout Layout { get; set; } = AeroCtaLayout.Card;

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}

public enum AeroCtaLayout
{
    Simple,
    Card,
    Split
}
