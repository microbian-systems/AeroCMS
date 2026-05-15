using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.ProductCards;

public static class ProductCard2BlockMapper
{
    public static NeoPageNode ToNode(ProductCard2Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.product-cards.2",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["title"] = JsonSerializer.SerializeToElement(block.Title),
            ["price"] = JsonSerializer.SerializeToElement(block.Price),
            ["colorCount"] = JsonSerializer.SerializeToElement(block.ColorCount),
            ["imageUrl"] = JsonSerializer.SerializeToElement(block.ImageUrl),
            ["imageHoverUrl"] = JsonSerializer.SerializeToElement(block.ImageHoverUrl),
            ["ctaUrl"] = JsonSerializer.SerializeToElement(block.CtaUrl)
        }
    };

    public static ProductCard2Block FromNode(NeoPageNode node) => new()
    {
        Title = GetString(node, "title", "Limited Edition Sports Trainer"),
        Price = GetString(node, "price", "$189.99"),
        ColorCount = GetString(node, "colorCount", "6 Colors"),
        ImageUrl = GetString(node, "imageUrl", "https://images.unsplash.com/photo-1600185365483-26d7a4cc7519?auto=format&fit=crop&q=80&w=1160"),
        ImageHoverUrl = GetString(node, "imageHoverUrl", "https://images.unsplash.com/photo-1600185365926-3a2ce3cdb9eb?auto=format&fit=crop&q=80&w=1160"),
        CtaUrl = GetString(node, "ctaUrl", "#")
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;
}
