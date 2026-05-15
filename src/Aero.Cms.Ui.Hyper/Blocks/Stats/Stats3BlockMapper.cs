using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Stats;

public static class Stats3BlockMapper
{
    public static NeoPageNode ToNode(Stats3Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.stats.3",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["title"] = JsonSerializer.SerializeToElement(block.Title),
            ["description"] = JsonSerializer.SerializeToElement(block.Description),
            ["stats"] = JsonSerializer.SerializeToElement(block.Stats)
        }
    };

    public static Stats3Block FromNode(NeoPageNode node) => new()
    {
        Title = GetString(node, "title", "Trusted by eCommerce Businesses"),
        Description = GetString(node, "description", "Lorem ipsum dolor sit amet consectetur adipisicing elit. Ratione dolores laborum labore provident impedit esse recusandae facere libero harum sequi."),
        Stats = node.Properties.TryGetValue("stats", out var element) && element.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<StatItem>>(element.GetRawText()) ?? Stats1Block.DefaultStats.Select(CloneStat).ToList()
            : Stats1Block.DefaultStats.Select(CloneStat).ToList()
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;

    private static StatItem CloneStat(StatItem stat) => new()
    {
        Label = stat.Label,
        Value = stat.Value
    };
}
