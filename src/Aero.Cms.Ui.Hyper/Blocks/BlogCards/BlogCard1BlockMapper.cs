using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.BlogCards;

public static class BlogCard1BlockMapper
{
    public static NeoPageNode ToNode(BlogCard1Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.blog-cards.1",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["imageUrl"] = JsonSerializer.SerializeToElement(block.ImageUrl),
            ["mainText"] = JsonSerializer.SerializeToElement(block.MainText),
            ["description"] = JsonSerializer.SerializeToElement(block.Description),
            ["publishedAt"] = JsonSerializer.SerializeToElement(block.PublishedAt),
            ["ctaUrl"] = JsonSerializer.SerializeToElement(block.CtaUrl)
        }
    };

    public static BlogCard1Block FromNode(NeoPageNode node) => new()
    {
        ImageUrl = GetString(node, "imageUrl", "https://images.unsplash.com/photo-1524758631624-e2822e304c36?auto=format&fit=crop&q=80&w=1160"),
        MainText = GetString(node, "mainText", "How to position your furniture for positivity"),
        Description = GetString(node, "description", "Lorem ipsum dolor sit amet, consectetur adipisicing elit."),
        PublishedAt = GetString(node, "publishedAt", "10th Oct 2022"),
        CtaUrl = GetString(node, "ctaUrl", "#")
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;
}
