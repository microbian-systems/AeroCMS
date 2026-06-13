using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Buttons;

public static class Button9BlockMapper
{
    public static NeoPageNode ToNode(Button9Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.buttons.9",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["text"] = JsonSerializer.SerializeToElement(block.Text),
            ["url"] = JsonSerializer.SerializeToElement(block.Url),
            ["offsetStyle"] = JsonSerializer.SerializeToElement(block.OffsetStyle)
        }
    };

    public static Button9Block FromNode(NeoPageNode node) => new()
    {
        Text = GetString(node, "text", "Download"),
        Url = GetString(node, "url", "#"),
        OffsetStyle = GetString(node, "offsetStyle", "hover-out")
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;
}
