using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Ctas;

/// <summary>
/// Represents a class for Cta3BlockMapper.
/// </summary>
public static class Cta3BlockMapper
{
        /// <summary>
    /// ToNode method.
    /// </summary>
public static NeoPageNode ToNode(Cta3Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.ctas.3",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["title"] = JsonSerializer.SerializeToElement(block.Title),
            ["description"] = JsonSerializer.SerializeToElement(block.Description),
            ["ctaText"] = JsonSerializer.SerializeToElement(block.CtaText),
            ["ctaUrl"] = JsonSerializer.SerializeToElement(block.CtaUrl),
            ["imageUrl"] = JsonSerializer.SerializeToElement(block.ImageUrl)
        }
    };

        /// <summary>
    /// FromNode method.
    /// </summary>
public static Cta3Block FromNode(NeoPageNode node) => new()
    {
        Title = GetString(node, "title", "Lorem, ipsum dolor sit amet consectetur adipisicing elit"),
        Description = GetString(node, "description", "Lorem ipsum dolor sit amet, consectetur adipiscing elit."),
        CtaText = GetString(node, "ctaText", "Get Started Today"),
        CtaUrl = GetString(node, "ctaUrl", "#"),
        ImageUrl = GetString(node, "imageUrl", "https://images.unsplash.com/photo-1484959014842-cd1d967a39cf?auto=format&fit=crop&q=80&w=1160")
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;
}
