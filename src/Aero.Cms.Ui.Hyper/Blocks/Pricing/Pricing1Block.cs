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
    public const string BlockTypeId = "hyper.pricing.1";

    public override string BlockType => BlockTypeId;

    public string Title { get; set; } = "Pricing Plans";
    public string Description { get; set; } = "Choose the right plan for your team.";
    public List<Pricing1Plan> Plans { get; set; } = DefaultPlans.Select(ClonePlan).ToList();

    public static readonly List<Pricing1Plan> DefaultPlans =
    [
        new() { Name = "Starter", Price = "20$", Features = ["10 users included", "2GB of storage", "Email support", "Help center access"], Highlighted = false },
        new() { Name = "Pro", Price = "30$", Features = ["20 users included", "5GB of storage", "Email support", "Help center access", "Phone support", "Community access"], Highlighted = true }
    ];

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

public sealed class Pricing1Plan
{
    public string Name { get; set; } = "";
    public string Price { get; set; } = "";
    public string Period { get; set; } = "/month";
    public List<string> Features { get; set; } = [];
    public string CtaText { get; set; } = "Get Started";
    public string CtaUrl { get; set; } = "#";
    public bool Highlighted { get; set; }
}
