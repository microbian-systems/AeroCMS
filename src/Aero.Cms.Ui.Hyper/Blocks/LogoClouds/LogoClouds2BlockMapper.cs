using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.LogoClouds;

public static class LogoClouds2BlockMapper
{
    public static NeoPageNode ToNode(LogoClouds2Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.logo-clouds.2",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["title"] = JsonSerializer.SerializeToElement(block.Title),
            ["description"] = JsonSerializer.SerializeToElement(block.Description),
            ["logoItems"] = JsonSerializer.SerializeToElement(block.LogoItems)
        }
    };

    public static LogoClouds2Block FromNode(NeoPageNode node) => new()
    {
        Title = GetString(node, "title", "Trusted by many"),
        Description = GetString(node, "description", "Lorem, ipsum dolor sit amet consectetur adipisicing elit."),
        LogoItems = node.Properties.TryGetValue("logoItems", out var element) && element.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<LogoCloudsLogoItem>>(element.GetRawText()) ?? LogoCloudsDefaults.CloneDefaults()
            : LogoCloudsDefaults.CloneDefaults()
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;
}
