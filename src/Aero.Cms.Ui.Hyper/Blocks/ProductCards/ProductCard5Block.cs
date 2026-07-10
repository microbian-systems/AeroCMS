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
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.product-cards.5";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string Title { get; set; } = "Small Headphones";
        /// <summary>
    /// Gets or sets the Price.
    /// </summary>
public string Price { get; set; } = "$299";
        /// <summary>
    /// Gets or sets the Subtitle.
    /// </summary>
public string Subtitle { get; set; } = "Space Grey";
        /// <summary>
    /// Gets or sets the Image Url.
    /// </summary>
public string ImageUrl { get; set; } = "https://images.unsplash.com/photo-1592921870789-04563d55041c?auto=format&fit=crop&q=80&w=1160";
        /// <summary>
    /// Gets or sets the Colors.
    /// </summary>
public List<ProductCard5Color> Colors { get; set; } = DefaultColors.Select(CloneColor).ToList();
        /// <summary>
    /// Gets or sets the Cta Url.
    /// </summary>
public string CtaUrl { get; set; } = "#";

        /// <summary>
    /// DefaultColors.
    /// </summary>
public static readonly List<ProductCard5Color> DefaultColors =
    [
        new() { Hex = "#595759", Name = "Space Gray" },
        new() { Hex = "#d2d3d4", Name = "Silver" },
        new() { Hex = "#d89f97", Name = "Pink" },
        new() { Hex = "#afbfab", Name = "Green" },
        new() { Hex = "#91a5bb", Name = "Blue" }
    ];

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);

    private static ProductCard5Color CloneColor(ProductCard5Color c) => new()
    {
        Hex = c.Hex,
        Name = c.Name
    };
}

/// <summary>
/// Represents a class for ProductCard5Color.
/// </summary>
public sealed class ProductCard5Color
{
        /// <summary>
    /// Gets or sets the Hex.
    /// </summary>
public string Hex { get; set; } = "";
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public string Name { get; set; } = "";
}
