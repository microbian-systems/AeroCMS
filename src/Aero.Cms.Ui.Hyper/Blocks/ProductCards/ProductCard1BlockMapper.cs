using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.ProductCards;

public static class ProductCard1BlockMapper
{
    public static NeoPageNode ToNode(ProductCard1Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.product-cards.1",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["title"] = JsonSerializer.SerializeToElement(block.Title),
            ["price"] = JsonSerializer.SerializeToElement(block.Price),
            ["imageUrl"] = JsonSerializer.SerializeToElement(block.ImageUrl),
            ["imageHoverUrl"] = JsonSerializer.SerializeToElement(block.ImageHoverUrl),
            ["ctaUrl"] = JsonSerializer.SerializeToElement(block.CtaUrl)
        }
    };

    public static ProductCard1Block FromNode(NeoPageNode node) => new()
    {
        Title = GetString(node, "title", "Limited Edition Sports Trainer"),
        Price = GetString(node, "price", "$189.99"),
        ImageUrl = GetString(node, "imageUrl", "https://images.unsplash.com/photo-1523381210434-271e8be1f52b?auto=format&fit=crop&q=80&w=1160"),
        ImageHoverUrl = GetString(node, "imageHoverUrl", "https://images.unsplash.com/photo-1523381140794-a1eef18a37c7?auto=format&fit=crop&q=80&w=1160"),
        CtaUrl = GetString(node, "ctaUrl", "#")
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;
}
