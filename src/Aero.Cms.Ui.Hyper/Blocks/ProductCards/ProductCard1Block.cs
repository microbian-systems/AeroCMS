using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.ProductCards;

/// <summary>
/// HyperUI Product Cards 1 — product card with image hover swap, title, and price.
/// Source: hyperui/public/examples/marketing/product-cards/1.html.
/// </summary>
[BlockMetadata(
    "hyper.product-cards.1",
    "Product Card 1",
    Category = "Hyper",
    Icon = "shopping-bag",
    SortOrder = 103,
    SchemaVersion = 1)]
public sealed class ProductCard1Block : BlockBase
{
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.product-cards.1";

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
    /// Gets or sets the Image Url.
    /// </summary>
public string ImageUrl { get; set; } = "https://images.unsplash.com/photo-1523381210434-271e8be1f52b?auto=format&fit=crop&q=80&w=1160";
        /// <summary>
    /// Gets or sets the Image Hover Url.
    /// </summary>
public string ImageHoverUrl { get; set; } = "https://images.unsplash.com/photo-1523381140794-a1eef18a37c7?auto=format&fit=crop&q=80&w=1160";
        /// <summary>
    /// Gets or sets the Cta Url.
    /// </summary>
public string CtaUrl { get; set; } = "#";

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
