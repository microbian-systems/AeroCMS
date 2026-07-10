using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.ProductCards;

/// <summary>
/// HyperUI Product Cards 2 — product card with image hover swap, title, price, and color count.
/// Source: hyperui/public/examples/marketing/product-cards/2.html.
/// </summary>
[BlockMetadata(
    "hyper.product-cards.2",
    "Product Card 2",
    Category = "Hyper",
    Icon = "shopping-bag",
    SortOrder = 104,
    SchemaVersion = 1)]
public sealed class ProductCard2Block : BlockBase
{
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.product-cards.2";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string Title { get; set; } = "Limited Edition Sports Trainer";
        /// <summary>
    /// Gets or sets the Price.
    /// </summary>
public string Price { get; set; } = "$189.99";
        /// <summary>
    /// Gets or sets the Color Count.
    /// </summary>
public string ColorCount { get; set; } = "6 Colors";
        /// <summary>
    /// Gets or sets the Image Url.
    /// </summary>
public string ImageUrl { get; set; } = "https://images.unsplash.com/photo-1600185365483-26d7a4cc7519?auto=format&fit=crop&q=80&w=1160";
        /// <summary>
    /// Gets or sets the Image Hover Url.
    /// </summary>
public string ImageHoverUrl { get; set; } = "https://images.unsplash.com/photo-1600185365926-3a2ce3cdb9eb?auto=format&fit=crop&q=80&w=1160";
        /// <summary>
    /// Gets or sets the Cta Url.
    /// </summary>
public string CtaUrl { get; set; } = "#";

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
