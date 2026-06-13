using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Buttons;

public static class Button3BlockMapper
{
    public static NeoPageNode ToNode(Button3Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.buttons.3",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["text"] = JsonSerializer.SerializeToElement(block.Text),
            ["url"] = JsonSerializer.SerializeToElement(block.Url),
            ["roundedStyle"] = JsonSerializer.SerializeToElement(block.RoundedStyle)
        }
    };

    public static Button3Block FromNode(NeoPageNode node) => new()
    {
        Text = GetString(node, "text", "Download"),
        Url = GetString(node, "url", "#"),
        RoundedStyle = GetString(node, "roundedStyle", "sm")
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;
}
