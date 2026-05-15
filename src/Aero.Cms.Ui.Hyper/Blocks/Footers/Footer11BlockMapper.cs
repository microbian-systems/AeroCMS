using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Footers;

public static class Footer11BlockMapper
{
    public static NeoPageNode ToNode(Footer11Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.footers.11",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["copyright"] = JsonSerializer.SerializeToElement(block.Copyright)
        }
    };

    public static Footer11Block FromNode(NeoPageNode node) => new()
    {
        Copyright = GetString(node, "copyright", "Copyright &copy; 2022. All rights reserved.")
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;
}
