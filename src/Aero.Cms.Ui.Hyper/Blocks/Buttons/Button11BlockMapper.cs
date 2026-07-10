using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Buttons;

/// <summary>
/// Represents a class for Button11BlockMapper.
/// </summary>
public static class Button11BlockMapper
{
        /// <summary>
    /// ToNode method.
    /// </summary>
public static NeoPageNode ToNode(Button11Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.buttons.11",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["text"] = JsonSerializer.SerializeToElement(block.Text),
            ["url"] = JsonSerializer.SerializeToElement(block.Url),
            ["style"] = JsonSerializer.SerializeToElement(block.Style)
        }
    };

        /// <summary>
    /// FromNode method.
    /// </summary>
public static Button11Block FromNode(NeoPageNode node) => new()
    {
        Text = GetString(node, "text", "Find out more"),
        Url = GetString(node, "url", "#"),
        Style = GetString(node, "style", "solid")
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;
}
