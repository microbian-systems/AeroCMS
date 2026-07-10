using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.BlogCards;

/// <summary>
/// Represents a class for BlogCard5BlockMapper.
/// </summary>
public static class BlogCard5BlockMapper
{
        /// <summary>
    /// ToNode method.
    /// </summary>
public static NeoPageNode ToNode(BlogCard5Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.blog-cards.5",
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

        /// <summary>
    /// FromNode method.
    /// </summary>
public static BlogCard5Block FromNode(NeoPageNode node) => new()
    {
        ImageUrl = GetString(node, "imageUrl", ""),
        MainText = GetString(node, "mainText", "Lorem ipsum dolor sit, amet consectetur adipisicing elit."),
        Description = GetString(node, "description", "Lorem ipsum dolor sit amet, consectetur adipisicing elit."),
        CtaText = GetString(node, "ctaText", "Find out more"),
        CtaUrl = GetString(node, "ctaUrl", "#")
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;
}
