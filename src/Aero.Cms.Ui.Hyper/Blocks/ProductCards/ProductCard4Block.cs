using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.ProductCards;

/// <summary>
/// HyperUI Product Cards 4 — simple product card with image, title, and price.
/// Source: hyperui/public/examples/marketing/product-cards/4.html.
/// </summary>
[BlockMetadata(
    "hyper.product-cards.4",
    "Product Card 4",
    Category = "Hyper",
    Icon = "shopping-bag",
    SortOrder = 106,
    SchemaVersion = 1)]
public sealed class ProductCard4Block : BlockBase
{
    public const string BlockTypeId = "hyper.product-cards.4";

    public override string BlockType => BlockTypeId;

    public string Title { get; set; } = "Simple Watch";
    public string Price { get; set; } = "$150";
    public string ImageUrl { get; set; } = "https://images.unsplash.com/photo-1592921870789-04563d55041c?auto=format&fit=crop&q=80&w=1160";
    public string CtaUrl { get; set; } = "#";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
