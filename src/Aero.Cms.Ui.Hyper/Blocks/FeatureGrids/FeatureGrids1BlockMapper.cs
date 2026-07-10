using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.FeatureGrids;

/// <summary>
/// Represents a class for FeatureGrids1BlockMapper.
/// </summary>
public static class FeatureGrids1BlockMapper
{
        /// <summary>
    /// ToNode method.
    /// </summary>
public static NeoPageNode ToNode(FeatureGrids1Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.feature-grids.1",
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
public static FeatureGrids1Block FromNode(NeoPageNode node) => new()
    {
        Title = GetString(node, "title", "Features for growth"),
        Description = GetString(node, "description", "Lorem ipsum dolor sit, amet consectetur adipisicing elit. Veritatis tenetur, nemo quam voluptas sunt impedit dolorem asperiores aliquid doloribus fugit."),
        Items = node.Properties.TryGetValue("items", out var element) && element.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<FeatureGrids1Item>>(element.GetRawText()) ?? FeatureGrids1Block.DefaultItems.Select(CloneItem).ToList()
            : FeatureGrids1Block.DefaultItems.Select(CloneItem).ToList()
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;

    private static FeatureGrids1Item CloneItem(FeatureGrids1Item item) => new()
    {
        Icon = item.Icon,
        Title = item.Title,
        Description = item.Description,
        LinkUrl = item.LinkUrl
    };
}
