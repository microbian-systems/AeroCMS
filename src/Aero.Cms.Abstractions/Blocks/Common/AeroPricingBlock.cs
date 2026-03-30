using Aero.Cms.Core.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Core.Blocks.Common;

/// <summary>
/// A pricing table block for displaying product plans.
/// </summary>
[BlockMetadata("aero_pricing", "Aero Pricing", Category = "Aero")]
public class AeroPricingBlock : BlockBase
{
    public override string BlockType => "aero_pricing";

    public string? Title { get; set; }
    public string? Description { get; set; }
    public List<AeroPricingPlan> Plans { get; set; } = new();
    public string? AeroLayout { get; set; } = "Monthly"; // Monthly, Yearly, Comparisons

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}

public class AeroPricingPlan
{
    public string? Name { get; set; }
    public string? Price { get; set; }
    public string? Period { get; set; }
    public string? Description { get; set; }
    public List<string> Features { get; set; } = new();
    public string? CtaText { get; set; }
    public string? CtaUrl { get; set; }
    public bool IsPopular { get; set; }
}
