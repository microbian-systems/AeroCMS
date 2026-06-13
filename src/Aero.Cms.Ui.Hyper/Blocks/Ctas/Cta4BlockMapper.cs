using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Ctas;

public static class Cta4BlockMapper
{
    public static NeoPageNode ToNode(Cta4Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.ctas.4",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["title"] = JsonSerializer.SerializeToElement(block.Title),
            ["description"] = JsonSerializer.SerializeToElement(block.Description),
            ["ctaText"] = JsonSerializer.SerializeToElement(block.CtaText),
            ["ctaUrl"] = JsonSerializer.SerializeToElement(block.CtaUrl),
            ["imageUrl"] = JsonSerializer.SerializeToElement(block.ImageUrl),
            ["imageUrl2"] = JsonSerializer.SerializeToElement(block.ImageUrl2)
        }
    };

    public static Cta4Block FromNode(NeoPageNode node) => new()
    {
        Title = GetString(node, "title", "Lorem, ipsum dolor sit amet consectetur adipisicing elit"),
        Description = GetString(node, "description", "Lorem ipsum dolor sit amet, consectetur adipiscing elit."),
        CtaText = GetString(node, "ctaText", "Get Started Today"),
        CtaUrl = GetString(node, "ctaUrl", "#"),
        ImageUrl = GetString(node, "imageUrl", "https://images.unsplash.com/photo-1621274790572-7c32596bc67f?auto=format&fit=crop&q=80&w=1160"),
        ImageUrl2 = GetString(node, "imageUrl2", "https://images.unsplash.com/photo-1567168544813-cc03465b4fa8?auto=format&fit=crop&q=80&w=1160")
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;
}
