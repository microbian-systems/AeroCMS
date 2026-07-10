using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Buttons;

/// <summary>
/// Represents a class for Button6BlockMapper.
/// </summary>
public static class Button6BlockMapper
{
        /// <summary>
    /// ToNode method.
    /// </summary>
public static NeoPageNode ToNode(Button6Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.buttons.6",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["text"] = JsonSerializer.SerializeToElement(block.Text),
            ["url"] = JsonSerializer.SerializeToElement(block.Url),
            ["style"] = JsonSerializer.SerializeToElement(block.Style),
            ["iconPosition"] = JsonSerializer.SerializeToElement(block.IconPosition)
        }
    };

        /// <summary>
    /// FromNode method.
    /// </summary>
public static Button6Block FromNode(NeoPageNode node) => new()
    {
        Text = GetString(node, "text", "Download"),
        Url = GetString(node, "url", "#"),
        Style = GetString(node, "style", "solid"),
        IconPosition = GetString(node, "iconPosition", "start")
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;
}
