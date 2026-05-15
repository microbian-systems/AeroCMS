using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.ProductCards;

public static class ProductCard4BlockMapper
{
    public static NeoPageNode ToNode(ProductCard4Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.product-cards.4",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["title"] = JsonSerializer.SerializeToElement(block.Title),
            ["price"] = JsonSerializer.SerializeToElement(block.Price),
            ["imageUrl"] = JsonSerializer.SerializeToElement(block.ImageUrl),
            ["ctaUrl"] = JsonSerializer.SerializeToElement(block.CtaUrl)
        }
    };

    public static ProductCard4Block FromNode(NeoPageNode node) => new()
    {
        Title = GetString(node, "title", "Simple Watch"),
        Price = GetString(node, "price", "$150"),
        ImageUrl = GetString(node, "imageUrl", "https://images.unsplash.com/photo-1592921870789-04563d55041c?auto=format&fit=crop&q=80&w=1160"),
        CtaUrl = GetString(node, "ctaUrl", "#")
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;
}
