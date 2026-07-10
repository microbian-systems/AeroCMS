using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.LogoClouds;

/// <summary>
/// Represents a class for LogoClouds1BlockMapper.
/// </summary>
public static class LogoClouds1BlockMapper
{
        /// <summary>
    /// ToNode method.
    /// </summary>
public static NeoPageNode ToNode(LogoClouds1Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.logo-clouds.1",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["logoItems"] = JsonSerializer.SerializeToElement(block.LogoItems)
        }
    };

        /// <summary>
    /// FromNode method.
    /// </summary>
public static LogoClouds1Block FromNode(NeoPageNode node) => new()
    {
        LogoItems = node.Properties.TryGetValue("logoItems", out var element) && element.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<LogoCloudsLogoItem>>(element.GetRawText()) ?? LogoCloudsDefaults.CloneDefaults()
            : LogoCloudsDefaults.CloneDefaults()
    };
}
