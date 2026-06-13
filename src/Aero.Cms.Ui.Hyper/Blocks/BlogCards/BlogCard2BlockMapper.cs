using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.BlogCards;

public static class BlogCard2BlockMapper
{
    public static NeoPageNode ToNode(BlogCard2Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.blog-cards.2",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["imageUrl"] = JsonSerializer.SerializeToElement(block.ImageUrl),
            ["mainText"] = JsonSerializer.SerializeToElement(block.MainText),
            ["description"] = JsonSerializer.SerializeToElement(block.Description),
            ["ctaUrl"] = JsonSerializer.SerializeToElement(block.CtaUrl)
        }
    };

    public static BlogCard2Block FromNode(NeoPageNode node) => new()
    {
        ImageUrl = GetString(node, "imageUrl", "https://images.unsplash.com/photo-1631451095765-2c91616fc9e6?auto=format&fit=crop&q=80&w=1160"),
        MainText = GetString(node, "mainText", "Finding the Journey to Mordor"),
        Description = GetString(node, "description", "Lorem ipsum dolor sit amet, consectetur adipisicing elit."),
        CtaUrl = GetString(node, "ctaUrl", "#")
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;
}
