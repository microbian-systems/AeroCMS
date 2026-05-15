using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.LogoClouds;

public static class LogoClouds4BlockMapper
{
    public static NeoPageNode ToNode(LogoClouds4Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.logo-clouds.4",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["logoItems"] = JsonSerializer.SerializeToElement(block.LogoItems)
        }
    };

    public static LogoClouds4Block FromNode(NeoPageNode node) => new()
    {
        LogoItems = node.Properties.TryGetValue("logoItems", out var element) && element.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<LogoCloudsLogoItem>>(element.GetRawText()) ?? LogoCloudsDefaults.CloneDefaults()
            : LogoCloudsDefaults.CloneDefaults()
    };
}
