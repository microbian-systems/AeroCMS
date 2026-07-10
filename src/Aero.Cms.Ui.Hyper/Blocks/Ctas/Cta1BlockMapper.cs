using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Ctas;

/// <summary>
/// Represents a class for Cta1BlockMapper.
/// </summary>
public static class Cta1BlockMapper
{
        /// <summary>
    /// ToNode method.
    /// </summary>
public static NeoPageNode ToNode(Cta1Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.ctas.1",
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
public static Cta1Block FromNode(NeoPageNode node) => new()
    {
        Title = GetString(node, "title", "Lorem, ipsum dolor sit amet consectetur adipisicing elit"),
        Description = GetString(node, "description", "Lorem ipsum dolor sit amet, consectetur adipiscing elit."),
        CtaText = GetString(node, "ctaText", "Get Started Today"),
        CtaUrl = GetString(node, "ctaUrl", "#"),
        ImageUrl = GetString(node, "imageUrl", "https://images.unsplash.com/photo-1464582883107-8adf2dca8a9f?auto=format&fit=crop&q=80&w=1160")
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;
}
