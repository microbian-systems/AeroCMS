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
    public const string BlockTypeId = "hyper.pricing.2";

    public override string BlockType => BlockTypeId;

    public List<Pricing2Plan> Plans { get; set; } = DefaultPlans.Select(ClonePlan).ToList();

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

public sealed class Pricing2Plan
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Price { get; set; } = "";
    public string Period { get; set; } = "/month";
    public string CtaText { get; set; } = "Get Started";
    public string CtaUrl { get; set; } = "#";
    public List<Pricing2Feature> Features { get; set; } = [];
}

public sealed class Pricing2Feature
{
    public string Text { get; set; } = "";
    public bool Included { get; set; }
}
