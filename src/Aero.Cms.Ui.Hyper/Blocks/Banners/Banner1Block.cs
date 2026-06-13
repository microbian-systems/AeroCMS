using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Banners;

/// <summary>
/// HyperUI Banner 1 — centered hero with CTA buttons.
/// Source: hyperui/public/examples/marketing/banners/1.html.
/// </summary>
[BlockMetadata(
    "hyper.banners.1",
    "Banner 1",
    Category = "Hyper",
    Icon = "image",
    SortOrder = 60,
    SchemaVersion = 1)]
public sealed class Banner1Block : BlockBase
{
    public const string BlockTypeId = "hyper.banners.1";

    public override string BlockType => BlockTypeId;

    public string Title { get; set; } = "Understand user flow and <strong class=\"text-indigo-600\"> increase </strong> conversions";
    public string Highlight { get; set; } = "increase";
    public string Description { get; set; } = "Lorem ipsum dolor sit amet, consectetur adipisicing elit. Eaque, nisi. Natus, provident accusamus impedit minima harum corporis iusto.";
    public string CtaText { get; set; } = "Get Started";
    public string CtaUrl { get; set; } = "#";
    public string CtaText2 { get; set; } = "Learn More";
    public string CtaUrl2 { get; set; } = "#";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
