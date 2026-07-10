using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Neo.Blocks.CenteredHero;

/// <summary>
/// Represents a class for CenteredHeroBlockMapper.
/// </summary>
public static class CenteredHeroBlockMapper
{
        /// <summary>
    /// ToNode method.
    /// </summary>
public static NeoPageNode ToNode(CenteredHeroBlock block) => new()
    {
        NodeId = string.Empty,
        CatalogId = CenteredHeroBlock.BlockTypeId,
        Kind = NeoPageNodeKind.Section,
        Properties = new Dictionary<string, JsonElement>
        {
            ["eyebrow"]        = JsonSerializer.SerializeToElement(block.Eyebrow),
            ["title"]          = JsonSerializer.SerializeToElement(block.Title),
            ["highlight"]      = JsonSerializer.SerializeToElement(block.Highlight),
            ["description"]    = JsonSerializer.SerializeToElement(block.Description),
            ["primaryText"]    = JsonSerializer.SerializeToElement(block.PrimaryText),
            ["primaryUrl"]     = JsonSerializer.SerializeToElement(block.PrimaryUrl),
            ["secondaryText"]  = JsonSerializer.SerializeToElement(block.SecondaryText),
            ["secondaryUrl"]   = JsonSerializer.SerializeToElement(block.SecondaryUrl),
            ["trustMarkers"]   = JsonSerializer.SerializeToElement(block.TrustMarkers),
        }
    };

        /// <summary>
    /// FromNode method.
    /// </summary>
public static CenteredHeroBlock FromNode(NeoPageNode node) => new()
    {
        Eyebrow       = GetString(node, "eyebrow",       "Introducing NeoUI v3"),
        Title         = GetString(node, "title",         "Build beautiful Blazor apps"),
        Highlight     = GetString(node, "highlight",      "faster than ever"),
        Description   = GetString(node, "description",   string.Empty),
        PrimaryText   = GetString(node, "primaryText",   "Get started for free"),
        PrimaryUrl    = GetString(node, "primaryUrl",    "#"),
        SecondaryText = GetString(node, "secondaryText", "View on GitHub"),
        SecondaryUrl  = GetString(node, "secondaryUrl",  "#"),
        TrustMarkers  = node.Properties.TryGetValue("trustMarkers", out var element)
            && element.ValueKind == JsonValueKind.Array
                ? JsonSerializer.Deserialize<List<string>>(element.GetRawText()) ?? []
                : [],
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;
}
