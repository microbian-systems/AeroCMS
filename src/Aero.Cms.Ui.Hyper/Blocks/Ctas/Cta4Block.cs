using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Ctas;

/// <summary>
/// HyperUI CTA 4 — two-column grid with text, blue CTA, and two images.
/// Source: hyperui/public/examples/marketing/ctas/4.html, 4-dark.html.
/// </summary>
[BlockMetadata(
    "hyper.ctas.4",
    "CTA 4",
    Category = "Hyper",
    Icon = "megaphone",
    SortOrder = 69,
    SchemaVersion = 1)]
public sealed class Cta4Block : BlockBase
{
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.ctas.4";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string Title { get; set; } = "Lorem, ipsum dolor sit amet consectetur adipisicing elit";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string Description { get; set; } = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Et, egestas tempus tellus etiam sed. Quam a scelerisque amet ullamcorper eu enim et fermentum, augue. Aliquet amet volutpat quisque ut interdum tincidunt duis.";
        /// <summary>
    /// Gets or sets the Cta Text.
    /// </summary>
public string CtaText { get; set; } = "Get Started Today";
        /// <summary>
    /// Gets or sets the Cta Url.
    /// </summary>
public string CtaUrl { get; set; } = "#";
        /// <summary>
    /// Gets or sets the Image Url.
    /// </summary>
public string ImageUrl { get; set; } = "https://images.unsplash.com/photo-1621274790572-7c32596bc67f?auto=format&fit=crop&q=80&w=1160";
        /// <summary>
    /// Gets or sets the Image Url2.
    /// </summary>
public string ImageUrl2 { get; set; } = "https://images.unsplash.com/photo-1567168544813-cc03465b4fa8?auto=format&fit=crop&q=80&w=1160";

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
