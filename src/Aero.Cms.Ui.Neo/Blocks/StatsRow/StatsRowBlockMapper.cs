using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Neo.Blocks.StatsRow;

public static class StatsRowBlockMapper
{
    private static readonly List<StatItem> DefaultStats =
    [
        new("10,000+", "Happy Users"),
        new("$2M+", "ARR Generated"),
        new("99.9%", "Uptime SLA"),
        new("4.9/5", "Average Rating"),
    ];

    public static NeoPageNode ToNode(StatsRowBlock block) => new()
    {
        NodeId = string.Empty,
        CatalogId = StatsRowBlock.BlockTypeId,
        Kind = NeoPageNodeKind.Section,
        Properties = new Dictionary<string, JsonElement>
        {
            ["stats"] = JsonSerializer.SerializeToElement(block.Stats),
        }
    };

    public static StatsRowBlock FromNode(NeoPageNode node) => new()
    {
        Stats = node.Properties.TryGetValue("stats", out var element)
            && element.ValueKind == JsonValueKind.Array
                ? JsonSerializer.Deserialize<List<StatItem>>(element.GetRawText()) ?? []
                : DefaultStats,
    };

}
