using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.FeatureGrids;

/// <summary>
/// Represents a class for FeatureGrids3BlockMapper.
/// </summary>
public static class FeatureGrids3BlockMapper
{
        /// <summary>
    /// ToNode method.
    /// </summary>
public static NeoPageNode ToNode(FeatureGrids3Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.feature-grids.3",
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
public static FeatureGrids3Block FromNode(NeoPageNode node) => new()
    {
        Title = GetString(node, "title", "Feature Grid 3"),
        Description = GetString(node, "description", "Features that matter."),
        Items = node.Properties.TryGetValue("items", out var element) && element.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<FeatureGrids3Item>>(element.GetRawText()) ?? FeatureGrids3Block.DefaultItems.Select(CloneItem).ToList()
            : FeatureGrids3Block.DefaultItems.Select(CloneItem).ToList()
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;

    private static FeatureGrids3Item CloneItem(FeatureGrids3Item item) => new()
    {
        Title = item.Title,
        Description = item.Description,
        SvgPath = item.SvgPath
    };
}
