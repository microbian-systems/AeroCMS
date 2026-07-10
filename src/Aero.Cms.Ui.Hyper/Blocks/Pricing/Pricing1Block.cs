using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Pricing;

/// <summary>
/// HyperUI Pricing 1 — side-by-side pricing cards with a highlighted plan.
/// Source: hyperui/public/examples/marketing/pricing/1.html.
/// </summary>
[BlockMetadata(
    "hyper.pricing.1",
    "Pricing 1",
    Category = "Hyper",
    Icon = "dollar-sign",
    SortOrder = 10,
    SchemaVersion = 1)]
public sealed class Pricing1Block : BlockBase
{
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.pricing.1";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string Title { get; set; } = "Pricing Plans";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string Description { get; set; } = "Choose the right plan for your team.";
        /// <summary>
    /// Gets or sets the Plans.
    /// </summary>
public List<Pricing1Plan> Plans { get; set; } = DefaultPlans.Select(ClonePlan).ToList();

        /// <summary>
    /// DefaultPlans.
    /// </summary>
public static readonly List<Pricing1Plan> DefaultPlans =
    [
        new() { Name = "Starter", Price = "20$", Features = ["10 users included", "2GB of storage", "Email support", "Help center access"], Highlighted = false },
        new() { Name = "Pro", Price = "30$", Features = ["20 users included", "5GB of storage", "Email support", "Help center access", "Phone support", "Community access"], Highlighted = true }
    ];

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);

    private static Pricing1Plan ClonePlan(Pricing1Plan plan) => new()
    {
        Name = plan.Name,
        Price = plan.Price,
        Period = plan.Period,
        Features = plan.Features.ToList(),
        CtaText = plan.CtaText,
        CtaUrl = plan.CtaUrl,
        Highlighted = plan.Highlighted
    };
}

/// <summary>
/// Represents a class for Pricing1Plan.
/// </summary>
public sealed class Pricing1Plan
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public string Name { get; set; } = "";
        /// <summary>
    /// Gets or sets the Price.
    /// </summary>
public string Price { get; set; } = "";
        /// <summary>
    /// Gets or sets the Period.
    /// </summary>
public string Period { get; set; } = "/month";
        /// <summary>
    /// Gets or sets the Features.
    /// </summary>
public List<string> Features { get; set; } = [];
        /// <summary>
    /// Gets or sets the Cta Text.
    /// </summary>
public string CtaText { get; set; } = "Get Started";
        /// <summary>
    /// Gets or sets the Cta Url.
    /// </summary>
public string CtaUrl { get; set; } = "#";
        /// <summary>
    /// Gets or sets the Highlighted.
    /// </summary>
public bool Highlighted { get; set; }
}
