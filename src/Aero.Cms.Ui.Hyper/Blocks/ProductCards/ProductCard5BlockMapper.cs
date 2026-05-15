using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.ProductCards;

public static class ProductCard5BlockMapper
{
    public static NeoPageNode ToNode(ProductCard5Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.product-cards.5",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["title"] = JsonSerializer.SerializeToElement(block.Title),
            ["price"] = JsonSerializer.SerializeToElement(block.Price),
            ["subtitle"] = JsonSerializer.SerializeToElement(block.Subtitle),
            ["imageUrl"] = JsonSerializer.SerializeToElement(block.ImageUrl),
            ["colors"] = JsonSerializer.SerializeToElement(block.Colors),
            ["ctaUrl"] = JsonSerializer.SerializeToElement(block.CtaUrl)
        }
    };

    public static ProductCard5Block FromNode(NeoPageNode node) => new()
    {
        Title = GetString(node, "title", "Small Headphones"),
        Price = GetString(node, "price", "$299"),
        Subtitle = GetString(node, "subtitle", "Space Grey"),
        ImageUrl = GetString(node, "imageUrl", "https://images.unsplash.com/photo-1592921870789-04563d55041c?auto=format&fit=crop&q=80&w=1160"),
        Colors = node.Properties.TryGetValue("colors", out var element) && element.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<ProductCard5Color>>(element.GetRawText()) ?? ProductCard5Block.DefaultColors.Select(CloneColor).ToList()
            : ProductCard5Block.DefaultColors.Select(CloneColor).ToList(),
        CtaUrl = GetString(node, "ctaUrl", "#")
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;

    private static ProductCard5Color CloneColor(ProductCard5Color c) => new()
    {
        Hex = c.Hex,
        Name = c.Name
    };
}
