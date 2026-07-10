using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.EmptyContent;

/// <summary>
/// Represents a class for EmptyContent3BlockMapper.
/// </summary>
public static class EmptyContent3BlockMapper
{
        /// <summary>
    /// ToNode method.
    /// </summary>
public static NeoPageNode ToNode(EmptyContent3Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.empty-content.3",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["title"] = JsonSerializer.SerializeToElement(block.Title),
            ["description"] = JsonSerializer.SerializeToElement(block.Description),
            ["emailPlaceholder"] = JsonSerializer.SerializeToElement(block.EmailPlaceholder),
            ["submitText"] = JsonSerializer.SerializeToElement(block.SubmitText),
            ["footnote"] = JsonSerializer.SerializeToElement(block.Footnote)
        }
    };

        /// <summary>
    /// FromNode method.
    /// </summary>
public static EmptyContent3Block FromNode(NeoPageNode node) => new()
    {
        Title = GetString(node, "title", "Coming soon!"),
        Description = GetString(node, "description", "We're working on something exciting."),
        EmailPlaceholder = GetString(node, "emailPlaceholder", "your@email.com"),
        SubmitText = GetString(node, "submitText", "Notify Me"),
        Footnote = GetString(node, "footnote", "We'll let you know the moment it's available.")
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;
}
