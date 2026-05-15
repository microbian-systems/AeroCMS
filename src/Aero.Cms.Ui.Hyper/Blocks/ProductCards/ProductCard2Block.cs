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
    public const string BlockTypeId = "hyper.product-cards.2";

    public override string BlockType => BlockTypeId;

    public string Title { get; set; } = "Limited Edition Sports Trainer";
    public string Price { get; set; } = "$189.99";
    public string ColorCount { get; set; } = "6 Colors";
    public string ImageUrl { get; set; } = "https://images.unsplash.com/photo-1600185365483-26d7a4cc7519?auto=format&fit=crop&q=80&w=1160";
    public string ImageHoverUrl { get; set; } = "https://images.unsplash.com/photo-1600185365926-3a2ce3cdb9eb?auto=format&fit=crop&q=80&w=1160";
    public string CtaUrl { get; set; } = "#";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
