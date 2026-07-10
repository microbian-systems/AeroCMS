using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Announcements;

/// <summary>
/// Represents a class for Announcement4BlockMapper.
/// </summary>
public static class Announcement4BlockMapper
{
        /// <summary>
    /// ToNode method.
    /// </summary>
public static NeoPageNode ToNode(Announcement4Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.announcements.4",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["mainText"] = JsonSerializer.SerializeToElement(block.MainText),
            ["ctaText"] = JsonSerializer.SerializeToElement(block.CtaText),
            ["ctaUrl"] = JsonSerializer.SerializeToElement(block.CtaUrl)
        }
    };

        /// <summary>
    /// FromNode method.
    /// </summary>
public static Announcement4Block FromNode(NeoPageNode node) => new()
    {
        MainText = GetString(node, "mainText", "Lorem, ipsum dolor"),
        CtaText = GetString(node, "ctaText", "sit amet consectetur"),
        CtaUrl = GetString(node, "ctaUrl", "#")
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;
}
