using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Buttons;

/// <summary>
/// Represents a class for Button8BlockMapper.
/// </summary>
public static class Button8BlockMapper
{
        /// <summary>
    /// ToNode method.
    /// </summary>
public static NeoPageNode ToNode(Button8Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.buttons.8",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["text"] = JsonSerializer.SerializeToElement(block.Text),
            ["url"] = JsonSerializer.SerializeToElement(block.Url),
            ["style"] = JsonSerializer.SerializeToElement(block.Style),
            ["rotateDirection"] = JsonSerializer.SerializeToElement(block.RotateDirection)
        }
    };

        /// <summary>
    /// FromNode method.
    /// </summary>
public static Button8Block FromNode(NeoPageNode node) => new()
    {
        Text = GetString(node, "text", "Download"),
        Url = GetString(node, "url", "#"),
        Style = GetString(node, "style", "solid"),
        RotateDirection = GetString(node, "rotateDirection", "positive")
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;
}
