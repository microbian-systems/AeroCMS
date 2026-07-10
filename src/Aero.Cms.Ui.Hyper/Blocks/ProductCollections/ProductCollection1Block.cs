using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.ProductCollections;

/// <summary>
/// HyperUI Product Collections 1 — four-column product grid with header.
/// Source: hyperui/public/examples/marketing/product-collections/1.html (light-only).
/// </summary>
[BlockMetadata(
    "hyper.product-collections.1",
    "Product Collections 1",
    Category = "Hyper",
    Icon = "grid",
    SortOrder = 114,
    SchemaVersion = 1)]
public sealed class ProductCollection1Block : BlockBase
{
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.product-collections.1";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string Title { get; set; } = "Product Collection";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string Description { get; set; } = "Lorem ipsum, dolor sit amet consectetur adipisicing elit. Itaque praesentium cumque iure dicta incidunt est ipsam, officia dolor fugit natus?";
        /// <summary>
    /// Gets or sets the Products.
    /// </summary>
public List<ProductCollectionItem> Products { get; set; } = DefaultProducts.Select(CloneProduct).ToList();

        /// <summary>
    /// DefaultProducts.
    /// </summary>
public static readonly List<ProductCollectionItem> DefaultProducts =
    [
        new() { Name = "Basic Tee", Price = "£24.00 GBP", ImageUrl = "https://images.unsplash.com/photo-1523381210434-271e8be1f52b?auto=format&fit=crop&q=80&w=1160", ProductUrl = "#" },
        new() { Name = "Basic Tee", Price = "£24.00 GBP", ImageUrl = "https://images.unsplash.com/photo-1523381210434-271e8be1f52b?auto=format&fit=crop&q=80&w=1160", ProductUrl = "#" },
        new() { Name = "Basic Tee", Price = "£24.00 GBP", ImageUrl = "https://images.unsplash.com/photo-1523381210434-271e8be1f52b?auto=format&fit=crop&q=80&w=1160", ProductUrl = "#" },
        new() { Name = "Basic Tee", Price = "£24.00 GBP", ImageUrl = "https://images.unsplash.com/photo-1523381210434-271e8be1f52b?auto=format&fit=crop&q=80&w=1160", ProductUrl = "#" }
    ];

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);

    private static ProductCollectionItem CloneProduct(ProductCollectionItem p) => new()
    {
        Name = p.Name,
        Price = p.Price,
        ImageUrl = p.ImageUrl,
        ProductUrl = p.ProductUrl
    };
}

/// <summary>
/// Represents a class for ProductCollectionItem.
/// </summary>
public sealed class ProductCollectionItem
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public string Name { get; set; } = "";
        /// <summary>
    /// Gets or sets the Price.
    /// </summary>
public string Price { get; set; } = "";
        /// <summary>
    /// Gets or sets the Image Url.
    /// </summary>
public string ImageUrl { get; set; } = "";
        /// <summary>
    /// Gets or sets the Product Url.
    /// </summary>
public string ProductUrl { get; set; } = "#";
}
