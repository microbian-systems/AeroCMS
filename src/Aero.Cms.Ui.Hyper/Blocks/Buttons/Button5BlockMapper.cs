using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Buttons;

/// <summary>
/// Represents a class for Button5BlockMapper.
/// </summary>
public static class Button5BlockMapper
{
        /// <summary>
    /// ToNode method.
    /// </summary>
public static NeoPageNode ToNode(Button5Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.buttons.5",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["text"] = JsonSerializer.SerializeToElement(block.Text),
            ["url"] = JsonSerializer.SerializeToElement(block.Url),
            ["offsetStyle"] = JsonSerializer.SerializeToElement(block.OffsetStyle)
        }
    };

        /// <summary>
    /// FromNode method.
    /// </summary>
public static Button5Block FromNode(NeoPageNode node) => new()
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
