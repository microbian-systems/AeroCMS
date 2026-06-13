using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Cards;

public static class Card7BlockMapper
{
    public static NeoPageNode ToNode(Card7Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.cards.7",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["title"] = JsonSerializer.SerializeToElement(block.Title),
            ["subtitle"] = JsonSerializer.SerializeToElement(block.Subtitle),
            ["imageUrl"] = JsonSerializer.SerializeToElement(block.ImageUrl),
            ["ctaUrl"] = JsonSerializer.SerializeToElement(block.CtaUrl)
        }
    };

    public static Card7Block FromNode(NeoPageNode node) => new()
    {
        Title = GetString(node, "title", "Company Name"),
        Subtitle = GetString(node, "subtitle", "Branding / Signage"),
        ImageUrl = GetString(node, "imageUrl", "https://images.unsplash.com/photo-1588515724527-074a7a56616c?auto=format&fit=crop&q=80&w=1160"),
        CtaUrl = GetString(node, "ctaUrl", "#")
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;
}
