using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.ProductCards;

public static class ProductCard7BlockMapper
{
    public static NeoPageNode ToNode(ProductCard7Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.product-cards.7",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["title"] = JsonSerializer.SerializeToElement(block.Title),
            ["description"] = JsonSerializer.SerializeToElement(block.Description),
            ["imageUrl"] = JsonSerializer.SerializeToElement(block.ImageUrl),
            ["badgeText"] = JsonSerializer.SerializeToElement(block.BadgeText),
            ["ctaText"] = JsonSerializer.SerializeToElement(block.CtaText),
            ["ctaUrl"] = JsonSerializer.SerializeToElement(block.CtaUrl)
        }
    };

    public static ProductCard7Block FromNode(NeoPageNode node) => new()
    {
        Title = GetString(node, "title", "Aloe Vera"),
        Description = GetString(node, "description", "Lorem ipsum dolor sit amet consectetur adipisicing elit. Amet officia rem vel voluptatum in eum vitae aliquid at sed dignissimos."),
        ImageUrl = GetString(node, "imageUrl", "https://images.unsplash.com/photo-1485955900006-10f4d324d411?auto=format&fit=crop&q=80&w=1160"),
        BadgeText = GetString(node, "badgeText", "Save 10%"),
        CtaText = GetString(node, "ctaText", "Buy now"),
        CtaUrl = GetString(node, "ctaUrl", "#")
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;
}
