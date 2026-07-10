using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.BlogCards;

/// <summary>
/// Represents a class for BlogCard4BlockMapper.
/// </summary>
public static class BlogCard4BlockMapper
{
        /// <summary>
    /// ToNode method.
    /// </summary>
public static NeoPageNode ToNode(BlogCard4Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.blog-cards.4",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["mainText"] = JsonSerializer.SerializeToElement(block.MainText),
            ["publishedAt"] = JsonSerializer.SerializeToElement(block.PublishedAt),
            ["tags"] = JsonSerializer.SerializeToElement(block.Tags),
            ["ctaUrl"] = JsonSerializer.SerializeToElement(block.CtaUrl)
        }
    };

        /// <summary>
    /// FromNode method.
    /// </summary>
public static BlogCard4Block FromNode(NeoPageNode node) => new()
    {
        MainText = GetString(node, "mainText", "How to center an element using JavaScript and jQuery"),
        PublishedAt = GetString(node, "publishedAt", "10th Oct 2022"),
        Tags = node.Properties.TryGetValue("tags", out var element) && element.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<string>>(element.GetRawText()) ?? ["Snippet", "JavaScript"]
            : ["Snippet", "JavaScript"],
        CtaUrl = GetString(node, "ctaUrl", "#")
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;
}
