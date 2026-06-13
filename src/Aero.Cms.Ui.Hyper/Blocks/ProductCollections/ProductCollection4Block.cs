using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.ProductCollections;

/// <summary>
/// HyperUI Product Collections 4 — product grid with sidebar filters.
/// Source: hyperui/public/examples/marketing/product-collections/4.html (light-only).
/// </summary>
[BlockMetadata(
    "hyper.product-collections.4",
    "Product Collections 4",
    Category = "Hyper",
    Icon = "grid",
    SortOrder = 117,
    SchemaVersion = 1)]
public sealed class ProductCollection4Block : BlockBase
{
    public const string BlockTypeId = "hyper.product-collections.4";

    public override string BlockType => BlockTypeId;

    public string Title { get; set; } = "Product Collection";
    public string Description { get; set; } = "Lorem ipsum, dolor sit amet consectetur adipisicing elit. Itaque praesentium cumque iure dicta incidunt est ipsam, officia dolor fugit natus?";
    public List<ProductCollectionItem> Products { get; set; } = ProductCollection1Block.DefaultProducts.Select(CloneProduct).ToList();

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);

    private static ProductCollectionItem CloneProduct(ProductCollectionItem p) => new()
    {
        Name = p.Name,
        Price = p.Price,
        ImageUrl = p.ImageUrl,
        ProductUrl = p.ProductUrl
    };
}
