using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.ProductCards;

/// <summary>
/// HyperUI Product Cards 5 — product card with image, color swatches, title, and price.
/// Source: hyperui/public/examples/marketing/product-cards/5.html.
/// </summary>
[BlockMetadata(
    "hyper.product-cards.5",
    "Product Card 5",
    Category = "Hyper",
    Icon = "shopping-bag",
    SortOrder = 107,
    SchemaVersion = 1)]
public sealed class ProductCard5Block : BlockBase
{
    public const string BlockTypeId = "hyper.product-cards.5";

    public override string BlockType => BlockTypeId;

    public string Title { get; set; } = "Small Headphones";
    public string Price { get; set; } = "$299";
    public string Subtitle { get; set; } = "Space Grey";
    public string ImageUrl { get; set; } = "https://images.unsplash.com/photo-1592921870789-04563d55041c?auto=format&fit=crop&q=80&w=1160";
    public List<ProductCard5Color> Colors { get; set; } = DefaultColors.Select(CloneColor).ToList();
    public string CtaUrl { get; set; } = "#";

    public static readonly List<ProductCard5Color> DefaultColors =
    [
        new() { Hex = "#595759", Name = "Space Gray" },
        new() { Hex = "#d2d3d4", Name = "Silver" },
        new() { Hex = "#d89f97", Name = "Pink" },
        new() { Hex = "#afbfab", Name = "Green" },
        new() { Hex = "#91a5bb", Name = "Blue" }
    ];

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);

    private static ProductCard5Color CloneColor(ProductCard5Color c) => new()
    {
        Hex = c.Hex,
        Name = c.Name
    };
}

public sealed class ProductCard5Color
{
    public string Hex { get; set; } = "";
    public string Name { get; set; } = "";
}
