using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.EmptyContent;

/// <summary>
/// Represents a class for EmptyContent4BlockMapper.
/// </summary>
public static class EmptyContent4BlockMapper
{
        /// <summary>
    /// ToNode method.
    /// </summary>
public static NeoPageNode ToNode(EmptyContent4Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.empty-content.4",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["title"] = JsonSerializer.SerializeToElement(block.Title),
            ["description"] = JsonSerializer.SerializeToElement(block.Description),
            ["ctaText"] = JsonSerializer.SerializeToElement(block.CtaText),
            ["ctaUrl"] = JsonSerializer.SerializeToElement(block.CtaUrl),
            ["links"] = JsonSerializer.SerializeToElement(block.Links)
        }
    };

        /// <summary>
    /// FromNode method.
    /// </summary>
public static EmptyContent4Block FromNode(NeoPageNode node) => new()
    {
        Title = GetString(node, "title", "Explore more"),
        Description = GetString(node, "description", "This section doesn't have content right now."),
        CtaText = GetString(node, "ctaText", "Back to Shopping"),
        CtaUrl = GetString(node, "ctaUrl", "#"),
        Links = node.Properties.TryGetValue("links", out var element) && element.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<EmptyContentLink>>(element.GetRawText()) ?? EmptyContent4Block.DefaultLinks.Select(CloneLink).ToList()
            : EmptyContent4Block.DefaultLinks.Select(CloneLink).ToList()
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;

    private static EmptyContentLink CloneLink(EmptyContentLink l) => new()
    {
        Title = l.Title,
        Description = l.Description,
        Url = l.Url
    };
}
