using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.ProductCards;

/// <summary>
/// HyperUI Product Cards 3 — product card with image, title, description, and price.
/// Source: hyperui/public/examples/marketing/product-cards/3.html.
/// </summary>
[BlockMetadata(
    "hyper.product-cards.3",
    "Product Card 3",
    Category = "Hyper",
    Icon = "shopping-bag",
    SortOrder = 105,
    SchemaVersion = 1)]
public sealed class ProductCard3Block : BlockBase
{
    public const string BlockTypeId = "hyper.product-cards.3";

    public override string BlockType => BlockTypeId;

    public string Title { get; set; } = "Small Headphones";
    public string Description { get; set; } = "Lorem ipsum dolor sit amet consectetur adipisicing elit. Quasi nobis, quia soluta quisquam voluptatem nemo.";
    public string Price { get; set; } = "$299";
    public string ImageUrl { get; set; } = "https://images.unsplash.com/photo-1592921870789-04563d55041c?auto=format&fit=crop&q=80&w=1160";
    public string CtaUrl { get; set; } = "#";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
