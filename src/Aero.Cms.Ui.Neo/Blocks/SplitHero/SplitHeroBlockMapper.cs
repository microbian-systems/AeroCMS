using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Neo.Blocks.SplitHero;

public static class SplitHeroBlockMapper
{
    public static NeoPageNode ToNode(SplitHeroBlock block) => new()
    {
        NodeId = string.Empty,
        CatalogId = SplitHeroBlock.BlockTypeId,
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["eyebrow"]        = JsonSerializer.SerializeToElement(block.Eyebrow),
            ["title"]          = JsonSerializer.SerializeToElement(block.Title),
            ["description"]    = JsonSerializer.SerializeToElement(block.Description),
            ["primaryText"]    = JsonSerializer.SerializeToElement(block.PrimaryText),
            ["primaryUrl"]     = JsonSerializer.SerializeToElement(block.PrimaryUrl),
            ["secondaryText"]  = JsonSerializer.SerializeToElement(block.SecondaryText),
            ["secondaryUrl"]   = JsonSerializer.SerializeToElement(block.SecondaryUrl),
            ["footnote"]       = JsonSerializer.SerializeToElement(block.Footnote),
        }
    };

    public static SplitHeroBlock FromNode(NeoPageNode node) => new()
    {
        Eyebrow       = GetString(node, "eyebrow",       "New — v2.0 is here"),
        Title         = GetString(node, "title",         "Build better products, ship faster"),
        Description   = GetString(node, "description",   string.Empty),
        PrimaryText   = GetString(node, "primaryText",   "Get started free"),
        PrimaryUrl    = GetString(node, "primaryUrl",    "#"),
        SecondaryText = GetString(node, "secondaryText", "Watch demo"),
        SecondaryUrl  = GetString(node, "secondaryUrl",  "#"),
        Footnote      = GetString(node, "footnote",      "No credit card required · Free 14-day trial"),
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;
}
