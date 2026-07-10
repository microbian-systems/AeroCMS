using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.ProductCollections;

/// <summary>
/// Represents a class for ProductCollection1BlockMapper.
/// </summary>
public static class ProductCollection1BlockMapper
{
        /// <summary>
    /// ToNode method.
    /// </summary>
public static NeoPageNode ToNode(ProductCollection1Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.product-collections.1",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["title"] = JsonSerializer.SerializeToElement(block.Title),
            ["description"] = JsonSerializer.SerializeToElement(block.Description),
            ["products"] = JsonSerializer.SerializeToElement(block.Products)
        }
    };

        /// <summary>
    /// FromNode method.
    /// </summary>
public static ProductCollection1Block FromNode(NeoPageNode node) => new()
    {
        Title = GetString(node, "title", "Product Collection"),
        Description = GetString(node, "description", "Lorem ipsum, dolor sit amet consectetur adipisicing elit."),
        Products = node.Properties.TryGetValue("products", out var element) && element.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<ProductCollectionItem>>(element.GetRawText()) ?? ProductCollection1Block.DefaultProducts.Select(CloneProduct).ToList()
            : ProductCollection1Block.DefaultProducts.Select(CloneProduct).ToList()
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;

    private static ProductCollectionItem CloneProduct(ProductCollectionItem p) => new()
    {
        Name = p.Name,
        Price = p.Price,
        ImageUrl = p.ImageUrl,
        ProductUrl = p.ProductUrl
    };
}
