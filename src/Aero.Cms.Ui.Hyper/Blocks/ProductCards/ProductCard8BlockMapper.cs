using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.ProductCards;

/// <summary>
/// Represents a class for ProductCard8BlockMapper.
/// </summary>
public static class ProductCard8BlockMapper
{
        /// <summary>
    /// ToNode method.
    /// </summary>
public static NeoPageNode ToNode(ProductCard8Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.product-cards.8",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["title"] = JsonSerializer.SerializeToElement(block.Title),
            ["description"] = JsonSerializer.SerializeToElement(block.Description),
            ["price"] = JsonSerializer.SerializeToElement(block.Price),
            ["comparePrice"] = JsonSerializer.SerializeToElement(block.ComparePrice),
            ["imageUrl"] = JsonSerializer.SerializeToElement(block.ImageUrl),
            ["ctaText"] = JsonSerializer.SerializeToElement(block.CtaText),
            ["ctaUrl"] = JsonSerializer.SerializeToElement(block.CtaUrl),
            ["ctaText2"] = JsonSerializer.SerializeToElement(block.CtaText2),
            ["ctaUrl2"] = JsonSerializer.SerializeToElement(block.CtaUrl2)
        }
    };

        /// <summary>
    /// FromNode method.
    /// </summary>
public static ProductCard8Block FromNode(NeoPageNode node) => new()
    {
        Title = GetString(node, "title", "Wireless Headphones"),
        Description = GetString(node, "description", "Lorem ipsum dolor sit amet consectetur adipisicing elit. Labore nobis iure obcaecati pariatur. Officiis qui, enim cupiditate aliquam corporis iste."),
        Price = GetString(node, "price", "$49.99"),
        ComparePrice = GetString(node, "comparePrice", "$80"),
        ImageUrl = GetString(node, "imageUrl", "https://images.unsplash.com/photo-1628202926206-c63a34b1618f?auto=format&fit=crop&q=80&w=1160"),
        CtaText = GetString(node, "ctaText", "Add to Cart"),
        CtaUrl = GetString(node, "ctaUrl", "#"),
        CtaText2 = GetString(node, "ctaText2", "Buy Now"),
        CtaUrl2 = GetString(node, "ctaUrl2", "#")
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;
}
