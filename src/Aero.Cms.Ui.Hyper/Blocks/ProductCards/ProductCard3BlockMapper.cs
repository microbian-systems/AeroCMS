using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.ProductCards;

/// <summary>
/// Represents a class for ProductCard3BlockMapper.
/// </summary>
public static class ProductCard3BlockMapper
{
        /// <summary>
    /// ToNode method.
    /// </summary>
public static NeoPageNode ToNode(ProductCard3Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.product-cards.3",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["title"] = JsonSerializer.SerializeToElement(block.Title),
            ["description"] = JsonSerializer.SerializeToElement(block.Description),
            ["price"] = JsonSerializer.SerializeToElement(block.Price),
            ["imageUrl"] = JsonSerializer.SerializeToElement(block.ImageUrl),
            ["ctaUrl"] = JsonSerializer.SerializeToElement(block.CtaUrl)
        }
    };

        /// <summary>
    /// FromNode method.
    /// </summary>
public static ProductCard3Block FromNode(NeoPageNode node) => new()
    {
        Title = GetString(node, "title", "Small Headphones"),
        Description = GetString(node, "description", "Lorem ipsum dolor sit amet consectetur adipisicing elit. Quasi nobis, quia soluta quisquam voluptatem nemo."),
        Price = GetString(node, "price", "$299"),
        ImageUrl = GetString(node, "imageUrl", "https://images.unsplash.com/photo-1592921870789-04563d55041c?auto=format&fit=crop&q=80&w=1160"),
        CtaUrl = GetString(node, "ctaUrl", "#")
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;
}
