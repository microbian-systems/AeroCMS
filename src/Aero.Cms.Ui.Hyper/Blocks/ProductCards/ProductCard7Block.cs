using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.ProductCards;

/// <summary>
/// HyperUI Product Cards 7 — product card with sale badge, image, title, description, buy now button.
/// Source: hyperui/public/examples/marketing/product-cards/7.html.
/// </summary>
[BlockMetadata(
    "hyper.product-cards.7",
    "Product Card 7",
    Category = "Hyper",
    Icon = "shopping-bag",
    SortOrder = 109,
    SchemaVersion = 1)]
public sealed class ProductCard7Block : BlockBase
{
    public const string BlockTypeId = "hyper.product-cards.7";

    public override string BlockType => BlockTypeId;

    public string Title { get; set; } = "Aloe Vera";
    public string Description { get; set; } = "Lorem ipsum dolor sit amet consectetur adipisicing elit. Amet officia rem vel voluptatum in eum vitae aliquid at sed dignissimos.";
    public string ImageUrl { get; set; } = "https://images.unsplash.com/photo-1485955900006-10f4d324d411?auto=format&fit=crop&q=80&w=1160";
    public string BadgeText { get; set; } = "Save 10%";
    public string CtaText { get; set; } = "Buy now";
    public string CtaUrl { get; set; } = "#";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
