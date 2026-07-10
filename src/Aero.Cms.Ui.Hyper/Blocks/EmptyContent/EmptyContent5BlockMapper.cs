using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.EmptyContent;

/// <summary>
/// Represents a class for EmptyContent5BlockMapper.
/// </summary>
public static class EmptyContent5BlockMapper
{
        /// <summary>
    /// ToNode method.
    /// </summary>
public static NeoPageNode ToNode(EmptyContent5Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.empty-content.5",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["title"] = JsonSerializer.SerializeToElement(block.Title),
            ["description"] = JsonSerializer.SerializeToElement(block.Description),
            ["ctaText"] = JsonSerializer.SerializeToElement(block.CtaText),
            ["ctaUrl"] = JsonSerializer.SerializeToElement(block.CtaUrl),
            ["ctaText2"] = JsonSerializer.SerializeToElement(block.CtaText2),
            ["ctaUrl2"] = JsonSerializer.SerializeToElement(block.CtaUrl2),
            ["statusText"] = JsonSerializer.SerializeToElement(block.StatusText)
        }
    };

        /// <summary>
    /// FromNode method.
    /// </summary>
public static EmptyContent5Block FromNode(NeoPageNode node) => new()
    {
        Title = GetString(node, "title", "Out of stock"),
        Description = GetString(node, "description", "This item is currently unavailable."),
        CtaText = GetString(node, "ctaText", "Notify When Available"),
        CtaUrl = GetString(node, "ctaUrl", "#"),
        CtaText2 = GetString(node, "ctaText2", "Explore Similar Products"),
        CtaUrl2 = GetString(node, "ctaUrl2", "#"),
        StatusText = GetString(node, "statusText", "Last restocked: 3 weeks ago")
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;
}
