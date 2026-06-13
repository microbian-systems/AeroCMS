using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Neo.Blocks.SplitHero;

[BlockMetadata(
    "neo.hero.split",
    "Hero Split Layout",
    Category = "Neo",
    Icon = "layout-dashboard",
    SortOrder = 20,
    SchemaVersion = 1)]
public sealed class SplitHeroBlock : BlockBase
{
    public const string BlockTypeId = "neo.hero.split";
    public override string BlockType => BlockTypeId;

    public string Eyebrow { get; set; } = "New — v2.0 is here";
    public string Title { get; set; } = "Build better products, ship faster";
    public string Description { get; set; } =
        "The all-in-one platform that helps your team design, develop, and deliver exceptional digital experiences without the complexity.";
    public string PrimaryText { get; set; } = "Get started free";
    public string PrimaryUrl { get; set; } = "#";
    public string SecondaryText { get; set; } = "Watch demo";
    public string SecondaryUrl { get; set; } = "#";
    public string Footnote { get; set; } = "No credit card required · Free 14-day trial";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
