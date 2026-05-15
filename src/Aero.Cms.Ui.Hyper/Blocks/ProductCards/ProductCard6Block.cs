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
    public const string BlockTypeId = "hyper.product-cards.6";

    public override string BlockType => BlockTypeId;

    public string Title { get; set; } = "Robot Toy";
    public string Price { get; set; } = "$14.99";
    public string ImageUrl { get; set; } = "https://images.unsplash.com/photo-1599481238640-4c1288750d7a?auto=format&fit=crop&q=80&w=1160";
    public string BadgeText { get; set; } = "New";
    public string CtaText { get; set; } = "Add to Cart";
    public string CtaUrl { get; set; } = "#";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
