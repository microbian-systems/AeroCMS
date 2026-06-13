using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.BlogCards;

public static class BlogCard3BlockMapper
{
    public static NeoPageNode ToNode(BlogCard3Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.blog-cards.3",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["imageUrl"] = JsonSerializer.SerializeToElement(block.ImageUrl),
            ["mainText"] = JsonSerializer.SerializeToElement(block.MainText),
            ["description"] = JsonSerializer.SerializeToElement(block.Description),
            ["ctaText"] = JsonSerializer.SerializeToElement(block.CtaText),
            ["ctaUrl"] = JsonSerializer.SerializeToElement(block.CtaUrl)
        }
    };

    public static BlogCard3Block FromNode(NeoPageNode node) => new()
    {
        ImageUrl = GetString(node, "imageUrl", "https://images.unsplash.com/photo-1600880292203-757bb62b4baf?auto=format&fit=crop&q=80&w=1160"),
        MainText = GetString(node, "mainText", "Lorem ipsum dolor sit amet consectetur adipisicing elit."),
        Description = GetString(node, "description", "Lorem ipsum dolor sit amet, consectetur adipisicing elit."),
        CtaText = GetString(node, "ctaText", "Find out more"),
        CtaUrl = GetString(node, "ctaUrl", "#")
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;
}
