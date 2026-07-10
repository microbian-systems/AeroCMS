using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Abstractions.Blocks.Common;

/// <summary>
/// A pricing table block for displaying product plans.
/// </summary>
[BlockMetadata("aero_pricing", "Aero Pricing", Category = "Aero")]
public class AeroPricingBlock : BlockBase
{
        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => "aero_pricing";

        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string? Title { get; set; }
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description { get; set; }
        /// <summary>
    /// Gets or sets the Plans.
    /// </summary>
public List<AeroPricingPlan> Plans { get; set; } = new();
        /// <summary>
    /// Gets or sets the Aero Layout.
    /// </summary>
public string? AeroLayout { get; set; } = "Monthly"; // Monthly, Yearly, Comparisons

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}

/// <summary>
/// Represents a class for AeroPricingPlan.
/// </summary>
public class AeroPricingPlan
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public string? Name { get; set; }
        /// <summary>
    /// Gets or sets the Price.
    /// </summary>
public string? Price { get; set; }
        /// <summary>
    /// Gets or sets the Period.
    /// </summary>
public string? Period { get; set; }
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description { get; set; }
        /// <summary>
    /// Gets or sets the Features.
    /// </summary>
public List<string> Features { get; set; } = new();
        /// <summary>
    /// Gets or sets the Cta Text.
    /// </summary>
public string? CtaText { get; set; }
        /// <summary>
    /// Gets or sets the Cta Url.
    /// </summary>
public string? CtaUrl { get; set; }
        /// <summary>
    /// Gets or sets the Is Popular.
    /// </summary>
public bool IsPopular { get; set; }
}
