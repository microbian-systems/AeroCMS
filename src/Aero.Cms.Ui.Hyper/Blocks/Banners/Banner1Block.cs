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
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.banners.1";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string Title { get; set; } = "Understand user flow and <strong class=\"text-indigo-600\"> increase </strong> conversions";
        /// <summary>
    /// Gets or sets the Highlight.
    /// </summary>
public string Highlight { get; set; } = "increase";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string Description { get; set; } = "Lorem ipsum dolor sit amet, consectetur adipisicing elit. Eaque, nisi. Natus, provident accusamus impedit minima harum corporis iusto.";
        /// <summary>
    /// Gets or sets the Cta Text.
    /// </summary>
public string CtaText { get; set; } = "Get Started";
        /// <summary>
    /// Gets or sets the Cta Url.
    /// </summary>
public string CtaUrl { get; set; } = "#";
        /// <summary>
    /// Gets or sets the Cta Text2.
    /// </summary>
public string CtaText2 { get; set; } = "Learn More";
        /// <summary>
    /// Gets or sets the Cta Url2.
    /// </summary>
public string CtaUrl2 { get; set; } = "#";

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
