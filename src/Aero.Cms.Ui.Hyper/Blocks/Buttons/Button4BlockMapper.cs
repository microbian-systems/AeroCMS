using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Buttons;

/// <summary>
/// Represents a class for Button4BlockMapper.
/// </summary>
public static class Button4BlockMapper
{
        /// <summary>
    /// ToNode method.
    /// </summary>
public static NeoPageNode ToNode(Button4Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.buttons.4",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["label"] = JsonSerializer.SerializeToElement(block.Label),
            ["url"] = JsonSerializer.SerializeToElement(block.Url),
            ["style"] = JsonSerializer.SerializeToElement(block.Style)
        }
    };

        /// <summary>
    /// FromNode method.
    /// </summary>
public static Button4Block FromNode(NeoPageNode node) => new()
    {
        Label = GetString(node, "label", "Download"),
        Url = GetString(node, "url", "#"),
        Style = GetString(node, "style", "solid")
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;
}
