using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.ProductCards;

/// <summary>
/// HyperUI Product Cards 6 — product card with wishlist button, image hover zoom, badge, title, price, add to cart.
/// Source: hyperui/public/examples/marketing/product-cards/6.html.
/// </summary>
[BlockMetadata(
    "hyper.product-cards.6",
    "Product Card 6",
    Category = "Hyper",
    Icon = "shopping-bag",
    SortOrder = 108,
    SchemaVersion = 1)]
public sealed class ProductCard6Block : BlockBase
{
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.product-cards.6";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string Title { get; set; } = "Robot Toy";
        /// <summary>
    /// Gets or sets the Price.
    /// </summary>
public string Price { get; set; } = "$14.99";
        /// <summary>
    /// Gets or sets the Image Url.
    /// </summary>
public string ImageUrl { get; set; } = "https://images.unsplash.com/photo-1599481238640-4c1288750d7a?auto=format&fit=crop&q=80&w=1160";
        /// <summary>
    /// Gets or sets the Badge Text.
    /// </summary>
public string BadgeText { get; set; } = "New";
        /// <summary>
    /// Gets or sets the Cta Text.
    /// </summary>
public string CtaText { get; set; } = "Add to Cart";
        /// <summary>
    /// Gets or sets the Cta Url.
    /// </summary>
public string CtaUrl { get; set; } = "#";

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
