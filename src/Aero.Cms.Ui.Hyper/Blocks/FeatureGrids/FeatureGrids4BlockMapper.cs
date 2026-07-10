using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.FeatureGrids;

/// <summary>
/// Represents a class for FeatureGrids4BlockMapper.
/// </summary>
public static class FeatureGrids4BlockMapper
{
        /// <summary>
    /// ToNode method.
    /// </summary>
public static NeoPageNode ToNode(FeatureGrids4Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.feature-grids.4",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["title"] = JsonSerializer.SerializeToElement(block.Title),
            ["description"] = JsonSerializer.SerializeToElement(block.Description),
            ["items"] = JsonSerializer.SerializeToElement(block.Items)
        }
    };

        /// <summary>
    /// FromNode method.
    /// </summary>
public static FeatureGrids4Block FromNode(NeoPageNode node) => new()
    {
        Title = GetString(node, "title", "Feature Grid 4"),
        Description = GetString(node, "description", "Features that matter."),
        Items = node.Properties.TryGetValue("items", out var element) && element.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<FeatureGrids4Item>>(element.GetRawText()) ?? FeatureGrids4Block.DefaultItems.Select(CloneItem).ToList()
            : FeatureGrids4Block.DefaultItems.Select(CloneItem).ToList()
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;

    private static FeatureGrids4Item CloneItem(FeatureGrids4Item item) => new()
    {
        Title = item.Title,
        Description = item.Description,
        SvgPath = item.SvgPath
    };
}
