using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Cards;

public static class Card5BlockMapper
{
    public static NeoPageNode ToNode(Card5Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.cards.5",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["title"] = JsonSerializer.SerializeToElement(block.Title),
            ["price"] = JsonSerializer.SerializeToElement(block.Price),
            ["imageUrl"] = JsonSerializer.SerializeToElement(block.ImageUrl),
            ["features"] = JsonSerializer.SerializeToElement(block.Features),
            ["ctaUrl"] = JsonSerializer.SerializeToElement(block.CtaUrl)
        }
    };

    public static Card5Block FromNode(NeoPageNode node) => new()
    {
        Title = GetString(node, "title", "123 Wallaby Avenue, Park Road"),
        Price = GetString(node, "price", "$240,000"),
        ImageUrl = GetString(node, "imageUrl", "https://images.unsplash.com/photo-1613545325278-f24b0cae1224?auto=format&fit=crop&q=80&w=1160"),
        Features = node.Properties.TryGetValue("features", out var element) && element.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<Card5Feature>>(element.GetRawText()) ?? Card5Block.DefaultFeatures.Select(CloneFeature).ToList()
            : Card5Block.DefaultFeatures.Select(CloneFeature).ToList(),
        CtaUrl = GetString(node, "ctaUrl", "#")
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;

    private static Card5Feature CloneFeature(Card5Feature f) => new()
    {
        Label = f.Label,
        Value = f.Value,
        SvgPath = f.SvgPath
    };
}
