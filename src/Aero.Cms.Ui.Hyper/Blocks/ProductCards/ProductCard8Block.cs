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
    public const string BlockTypeId = "hyper.product-cards.8";

    public override string BlockType => BlockTypeId;

    public string Title { get; set; } = "Wireless Headphones";
    public string Description { get; set; } = "Lorem ipsum dolor sit amet consectetur adipisicing elit. Labore nobis iure obcaecati pariatur. Officiis qui, enim cupiditate aliquam corporis iste.";
    public string Price { get; set; } = "$49.99";
    public string ComparePrice { get; set; } = "$80";
    public string ImageUrl { get; set; } = "https://images.unsplash.com/photo-1628202926206-c63a34b1618f?auto=format&fit=crop&q=80&w=1160";
    public string CtaText { get; set; } = "Add to Cart";
    public string CtaUrl { get; set; } = "#";
    public string CtaText2 { get; set; } = "Buy Now";
    public string CtaUrl2 { get; set; } = "#";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
