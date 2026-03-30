using Aero.Cms.Core.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Core.Blocks.Common;

/// <summary>
/// A frequently asked questions (FAQ) block with collapsing or grid layouts.
/// </summary>
[BlockMetadata("aero_faq", "Aero FAQ", Category = "Aero")]
public class AeroFaqBlock : BlockBase
{
    public override string BlockType => "aero_faq";

    public string? Title { get; set; }
    public string? Description { get; set; }
    public List<AeroFaqItem> Items { get; set; } = new();
    public AeroFaqLayout Layout { get; set; } = AeroFaqLayout.Collapse;

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}

public class AeroFaqItem
{
    public string? Question { get; set; }
    public string? Answer { get; set; }
}

public enum AeroFaqLayout
{
    Collapse,
    Grid,
    Centered
}
