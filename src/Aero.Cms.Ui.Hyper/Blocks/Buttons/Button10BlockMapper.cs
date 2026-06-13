using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Buttons;

public static class Button10BlockMapper
{
    public static NeoPageNode ToNode(Button10Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.buttons.10",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["text"] = JsonSerializer.SerializeToElement(block.Text),
            ["url"] = JsonSerializer.SerializeToElement(block.Url),
            ["revealDirection"] = JsonSerializer.SerializeToElement(block.RevealDirection)
        }
    };

    public static Button10Block FromNode(NeoPageNode node) => new()
    {
        Text = GetString(node, "text", "Download"),
        Url = GetString(node, "url", "#"),
        RevealDirection = GetString(node, "revealDirection", "left")
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;
}
