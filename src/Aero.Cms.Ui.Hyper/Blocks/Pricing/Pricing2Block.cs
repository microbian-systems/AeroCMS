using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Pricing;

/// <summary>
/// HyperUI Pricing 2 — side-by-side pricing cards with included/not-included features.
/// Source: hyperui/public/examples/marketing/pricing/2.html (light-only).
/// </summary>
[BlockMetadata(
    "hyper.pricing.2",
    "Pricing 2",
    Category = "Hyper",
    Icon = "dollar-sign",
    SortOrder = 11,
    SchemaVersion = 1)]
public sealed class Pricing2Block : BlockBase
{
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.pricing.2";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Plans.
    /// </summary>
public List<Pricing2Plan> Plans { get; set; } = DefaultPlans.Select(ClonePlan).ToList();

        /// <summary>
    /// DefaultPlans.
    /// </summary>
public static readonly List<Pricing2Plan> DefaultPlans =
    [
        new()
        {
            Name = "Starter",
            Description = "Lorem ipsum dolor sit amet consectetur adipisicing elit.",
            Price = "20$",
            Period = "/month",
            CtaText = "Get Started",
            Features =
            [
                new() { Text = "10 users included", Included = true },
                new() { Text = "2GB of storage", Included = true },
                new() { Text = "Email support", Included = true },
                new() { Text = "Help center access", Included = false },
                new() { Text = "Phone support", Included = false },
                new() { Text = "Community access", Included = false }
            ]
        },
        new()
        {
            Name = "Pro",
            Description = "Lorem ipsum dolor sit amet consectetur adipisicing elit.",
            Price = "30$",
            Period = "/month",
            CtaText = "Get Started",
            Features =
            [
                new() { Text = "20 users included", Included = true },
                new() { Text = "5GB of storage", Included = true },
                new() { Text = "Email support", Included = true },
                new() { Text = "Help center access", Included = true },
                new() { Text = "Phone support", Included = false },
                new() { Text = "Community access", Included = false }
            ]
        },
        new()
        {
            Name = "Enterprise",
            Description = "Lorem ipsum dolor sit amet consectetur adipisicing elit.",
            Price = "100$",
            Period = "/month",
            CtaText = "Get Started",
            Features =
            [
                new() { Text = "50 users included", Included = true },
                new() { Text = "20GB of storage", Included = true },
                new() { Text = "Email support", Included = true },
                new() { Text = "Help center access", Included = true },
                new() { Text = "Phone support", Included = true },
                new() { Text = "Community access", Included = true }
            ]
        }
    ];

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);

    private static Pricing2Plan ClonePlan(Pricing2Plan plan) => new()
    {
        Name = plan.Name,
        Description = plan.Description,
        Price = plan.Price,
        Period = plan.Period,
        CtaText = plan.CtaText,
        CtaUrl = plan.CtaUrl,
        Features = plan.Features.Select(f => new Pricing2Feature { Text = f.Text, Included = f.Included }).ToList()
    };
}

/// <summary>
/// Represents a class for Pricing2Plan.
/// </summary>
public sealed class Pricing2Plan
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public string Name { get; set; } = "";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string Description { get; set; } = "";
        /// <summary>
    /// Gets or sets the Price.
    /// </summary>
public string Price { get; set; } = "";
        /// <summary>
    /// Gets or sets the Period.
    /// </summary>
public string Period { get; set; } = "/month";
        /// <summary>
    /// Gets or sets the Cta Text.
    /// </summary>
public string CtaText { get; set; } = "Get Started";
        /// <summary>
    /// Gets or sets the Cta Url.
    /// </summary>
public string CtaUrl { get; set; } = "#";
        /// <summary>
    /// Gets or sets the Features.
    /// </summary>
public List<Pricing2Feature> Features { get; set; } = [];
}

/// <summary>
/// Represents a class for Pricing2Feature.
/// </summary>
public sealed class Pricing2Feature
{
        /// <summary>
    /// Gets or sets the Text.
    /// </summary>
public string Text { get; set; } = "";
        /// <summary>
    /// Gets or sets the Included.
    /// </summary>
public bool Included { get; set; }
}
