using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.EmptyContent;

/// <summary>
/// Represents a class for EmptyContent2BlockMapper.
/// </summary>
public static class EmptyContent2BlockMapper
{
        /// <summary>
    /// ToNode method.
    /// </summary>
public static NeoPageNode ToNode(EmptyContent2Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.empty-content.2",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["title"] = JsonSerializer.SerializeToElement(block.Title),
            ["description"] = JsonSerializer.SerializeToElement(block.Description),
            ["ctaText"] = JsonSerializer.SerializeToElement(block.CtaText),
            ["ctaUrl"] = JsonSerializer.SerializeToElement(block.CtaUrl),
            ["ctaText2"] = JsonSerializer.SerializeToElement(block.CtaText2),
            ["ctaUrl2"] = JsonSerializer.SerializeToElement(block.CtaUrl2)
        }
    };

        /// <summary>
    /// FromNode method.
    /// </summary>
public static EmptyContent2Block FromNode(NeoPageNode node) => new()
    {
        Title = GetString(node, "title", "Hmm, nothing found"),
        Description = GetString(node, "description", "We couldn't find what you were looking for."),
        CtaText = GetString(node, "ctaText", "Browse Popular Items"),
        CtaUrl = GetString(node, "ctaUrl", "#"),
        CtaText2 = GetString(node, "ctaText2", "Refine Search"),
        CtaUrl2 = GetString(node, "ctaUrl2", "#")
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;
}
