using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Neo.Blocks.CtaBanner;

[BlockMetadata(
    "neo.cta.banner",
    "CTA Banner",
    Category = "Neo",
    Icon = "megaphone",
    SortOrder = 30,
    SchemaVersion = 1)]
public sealed class CtaBannerBlock : BlockBase
{
    public const string BlockTypeId = "neo.cta.banner";
    public override string BlockType => BlockTypeId;

    public string Title { get; set; } = "Start building for free today";
    public string Description { get; set; } = "Join thousands of teams already using Acme to ship faster and smarter. No credit card required.";
    public string PrimaryText { get; set; } = "Get started free";
    public string PrimaryUrl { get; set; } = "#";
    public string SecondaryText { get; set; } = "Schedule a demo";
    public string SecondaryUrl { get; set; } = "#";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
