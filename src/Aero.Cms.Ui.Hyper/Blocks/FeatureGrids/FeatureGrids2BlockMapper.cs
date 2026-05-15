using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.FeatureGrids;

public static class FeatureGrids2BlockMapper
{
    public static NeoPageNode ToNode(FeatureGrids2Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.feature-grids.2",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["title"] = JsonSerializer.SerializeToElement(block.Title),
            ["description"] = JsonSerializer.SerializeToElement(block.Description),
            ["items"] = JsonSerializer.SerializeToElement(block.Items)
        }
    };

    public static FeatureGrids2Block FromNode(NeoPageNode node) => new()
    {
        Title = GetString(node, "title", "Features for growth"),
        Description = GetString(node, "description", "Lorem ipsum dolor sit, amet consectetur adipisicing elit. Veritatis tenetur, nemo quam voluptas sunt impedit dolorem asperiores aliquid doloribus fugit."),
        Items = node.Properties.TryGetValue("items", out var element) && element.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<FeatureGrids2Item>>(element.GetRawText()) ?? FeatureGrids2Block.DefaultItems.Select(CloneItem).ToList()
            : FeatureGrids2Block.DefaultItems.Select(CloneItem).ToList()
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;

    private static FeatureGrids2Item CloneItem(FeatureGrids2Item item) => new()
    {
        Icon = item.Icon,
        Title = item.Title,
        Description = item.Description,
        LinkUrl = item.LinkUrl
    };
}
