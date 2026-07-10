using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Banners;

/// <summary>
/// Represents a class for Banner2BlockMapper.
/// </summary>
public static class Banner2BlockMapper
{
        /// <summary>
    /// ToNode method.
    /// </summary>
public static NeoPageNode ToNode(Banner2Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.banners.2",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["title"] = JsonSerializer.SerializeToElement(block.Title),
            ["highlight"] = JsonSerializer.SerializeToElement(block.Highlight),
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
public static Banner2Block FromNode(NeoPageNode node) => new()
    {
        Title = GetString(node, "title", "Understand user flow and <strong class=\"text-indigo-600\"> increase </strong> conversions"),
        Highlight = GetString(node, "highlight", "increase"),
        Description = GetString(node, "description", "Lorem ipsum dolor sit amet, consectetur adipisicing elit. Eaque, nisi. Natus, provident accusamus impedit minima harum corporis iusto."),
        CtaText = GetString(node, "ctaText", "Get Started"),
        CtaUrl = GetString(node, "ctaUrl", "#"),
        CtaText2 = GetString(node, "ctaText2", "Learn More"),
        CtaUrl2 = GetString(node, "ctaUrl2", "#")
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;
}
