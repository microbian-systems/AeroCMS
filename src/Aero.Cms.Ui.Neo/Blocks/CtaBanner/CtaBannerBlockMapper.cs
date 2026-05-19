using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Neo.Blocks.CtaBanner;

public static class CtaBannerBlockMapper
{
    public static NeoPageNode ToNode(CtaBannerBlock block) => new()
    {
        NodeId = string.Empty,
        CatalogId = CtaBannerBlock.BlockTypeId,
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["title"]          = JsonSerializer.SerializeToElement(block.Title),
            ["description"]    = JsonSerializer.SerializeToElement(block.Description),
            ["primaryText"]    = JsonSerializer.SerializeToElement(block.PrimaryText),
            ["primaryUrl"]     = JsonSerializer.SerializeToElement(block.PrimaryUrl),
            ["secondaryText"]  = JsonSerializer.SerializeToElement(block.SecondaryText),
            ["secondaryUrl"]   = JsonSerializer.SerializeToElement(block.SecondaryUrl),
        }
    };

    public static CtaBannerBlock FromNode(NeoPageNode node) => new()
    {
        Title         = GetString(node, "title",         "Start building for free today"),
        Description   = GetString(node, "description",   string.Empty),
        PrimaryText   = GetString(node, "primaryText",   "Get started free"),
        PrimaryUrl    = GetString(node, "primaryUrl",    "#"),
        SecondaryText = GetString(node, "secondaryText", "Schedule a demo"),
        SecondaryUrl  = GetString(node, "secondaryUrl",  "#"),
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;
}
