using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.ProductCards;

/// <summary>
/// HyperUI Product Cards 8 — product card with wishlist button, image, price with strikethrough, title, description, two CTA buttons.
/// Source: hyperui/public/examples/marketing/product-cards/8.html.
/// </summary>
[BlockMetadata(
    "hyper.product-cards.8",
    "Product Card 8",
    Category = "Hyper",
    Icon = "shopping-bag",
    SortOrder = 110,
    SchemaVersion = 1)]
public sealed class ProductCard8Block : BlockBase
{
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.product-cards.8";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string Title { get; set; } = "Wireless Headphones";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string Description { get; set; } = "Lorem ipsum dolor sit amet consectetur adipisicing elit. Labore nobis iure obcaecati pariatur. Officiis qui, enim cupiditate aliquam corporis iste.";
        /// <summary>
    /// Gets or sets the Price.
    /// </summary>
public string Price { get; set; } = "$49.99";
        /// <summary>
    /// Gets or sets the Compare Price.
    /// </summary>
public string ComparePrice { get; set; } = "$80";
        /// <summary>
    /// Gets or sets the Image Url.
    /// </summary>
public string ImageUrl { get; set; } = "https://images.unsplash.com/photo-1628202926206-c63a34b1618f?auto=format&fit=crop&q=80&w=1160";
        /// <summary>
    /// Gets or sets the Cta Text.
    /// </summary>
public string CtaText { get; set; } = "Add to Cart";
        /// <summary>
    /// Gets or sets the Cta Url.
    /// </summary>
public string CtaUrl { get; set; } = "#";
        /// <summary>
    /// Gets or sets the Cta Text2.
    /// </summary>
public string CtaText2 { get; set; } = "Buy Now";
        /// <summary>
    /// Gets or sets the Cta Url2.
    /// </summary>
public string CtaUrl2 { get; set; } = "#";

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
