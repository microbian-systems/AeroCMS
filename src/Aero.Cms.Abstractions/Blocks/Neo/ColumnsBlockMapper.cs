using System.Text.Json;

namespace Aero.Cms.Abstractions.Blocks.Neo;

public static class NeoColumnsBlockMapper
{
    public static NeoPageNode ToNode(NeoColumnsBlock block) => new()
    {
        CatalogId = "neo.layout.columns", Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["gap"] = JsonSerializer.SerializeToElement(block.Gap),
            ["equalHeight"] = JsonSerializer.SerializeToElement(block.EqualHeight)
        },
        Children = block.Items.Select((item, i) => new NeoPageNode
        {
            NodeId = $"col-{i}",
            CatalogId = "neo.layout.column",
            Kind = NeoPageNodeKind.Section,
            Properties = new Dictionary<string, JsonElement>
            {
                ["content"] = JsonSerializer.SerializeToElement(item.Content),
                ["span"] = JsonSerializer.SerializeToElement(item.Span)
            }
        }).ToList()
    };

    public static NeoColumnsBlock FromNode(NeoPageNode node) => new()
    {
        Gap = GetInt(node, "gap", 4),
        EqualHeight = GetBool(node, "equalHeight", true),
        Items = node.Children.Select(c => new ColumnItem
        {
            Content = c.Properties.TryGetValue("content", out var v) ? v.GetString() ?? "" : "",
            Span = c.Properties.TryGetValue("span", out var s) && s.TryGetInt32(out var n) ? n : 6
        }).ToList()
    };

    private static int GetInt(NeoPageNode n, string k, int d) =>
        n.Properties.TryGetValue(k, out var v) && v.TryGetInt32(out var x) ? x : d;
    private static bool GetBool(NeoPageNode n, string k, bool d) =>
        n.Properties.TryGetValue(k, out var v) ? v.ValueKind != JsonValueKind.False : d;
}
