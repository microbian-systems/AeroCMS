using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.BlogCards;

/// <summary>
/// Represents a class for BlogCard6BlockMapper.
/// </summary>
public static class BlogCard6BlockMapper
{
        /// <summary>
    /// ToNode method.
    /// </summary>
public static NeoPageNode ToNode(BlogCard6Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.blog-cards.6",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["imageUrl"] = JsonSerializer.SerializeToElement(block.ImageUrl),
            ["mainText"] = JsonSerializer.SerializeToElement(block.MainText),
            ["description"] = JsonSerializer.SerializeToElement(block.Description),
            ["publishedAt"] = JsonSerializer.SerializeToElement(block.PublishedAt),
            ["publishedAtDay"] = JsonSerializer.SerializeToElement(block.PublishedAtDay),
            ["ctaText"] = JsonSerializer.SerializeToElement(block.CtaText),
            ["ctaUrl"] = JsonSerializer.SerializeToElement(block.CtaUrl)
        }
    };

        /// <summary>
    /// FromNode method.
    /// </summary>
public static BlogCard6Block FromNode(NeoPageNode node) => new()
    {
        ImageUrl = GetString(node, "imageUrl", "https://images.unsplash.com/photo-1609557927087-f9cf8e88de18?auto=format&fit=crop&q=80&w=1160"),
        MainText = GetString(node, "mainText", "Finding the right guitar for your style - 5 tips"),
        Description = GetString(node, "description", "Lorem ipsum dolor sit amet, consectetur adipisicing elit."),
        PublishedAt = GetString(node, "publishedAt", "2022"),
        PublishedAtDay = GetString(node, "publishedAtDay", "Oct 10"),
        CtaText = GetString(node, "ctaText", "Read Blog"),
        CtaUrl = GetString(node, "ctaUrl", "#")
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;
}
