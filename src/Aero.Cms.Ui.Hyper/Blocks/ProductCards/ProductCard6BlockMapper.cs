using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.ProductCards;

/// <summary>
/// Represents a class for ProductCard6BlockMapper.
/// </summary>
public static class ProductCard6BlockMapper
{
        /// <summary>
    /// ToNode method.
    /// </summary>
public static NeoPageNode ToNode(ProductCard6Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.product-cards.6",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["title"] = JsonSerializer.SerializeToElement(block.Title),
            ["price"] = JsonSerializer.SerializeToElement(block.Price),
            ["imageUrl"] = JsonSerializer.SerializeToElement(block.ImageUrl),
            ["badgeText"] = JsonSerializer.SerializeToElement(block.BadgeText),
            ["ctaText"] = JsonSerializer.SerializeToElement(block.CtaText),
            ["ctaUrl"] = JsonSerializer.SerializeToElement(block.CtaUrl)
        }
    };

        /// <summary>
    /// FromNode method.
    /// </summary>
public static ProductCard6Block FromNode(NeoPageNode node) => new()
    {
        Title = GetString(node, "title", "Robot Toy"),
        Price = GetString(node, "price", "$14.99"),
        ImageUrl = GetString(node, "imageUrl", "https://images.unsplash.com/photo-1599481238640-4c1288750d7a?auto=format&fit=crop&q=80&w=1160"),
        BadgeText = GetString(node, "badgeText", "New"),
        CtaText = GetString(node, "ctaText", "Add to Cart"),
        CtaUrl = GetString(node, "ctaUrl", "#")
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;
}
