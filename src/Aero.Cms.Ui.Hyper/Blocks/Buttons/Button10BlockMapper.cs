using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Buttons;

/// <summary>
/// Represents a class for Button10BlockMapper.
/// </summary>
public static class Button10BlockMapper
{
        /// <summary>
    /// ToNode method.
    /// </summary>
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

        /// <summary>
    /// FromNode method.
    /// </summary>
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
