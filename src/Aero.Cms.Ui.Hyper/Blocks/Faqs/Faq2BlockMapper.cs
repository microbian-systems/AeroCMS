using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Faqs;

/// <summary>
/// Represents a class for Faq2BlockMapper.
/// </summary>
public static class Faq2BlockMapper
{
        /// <summary>
    /// ToNode method.
    /// </summary>
public static NeoPageNode ToNode(Faq2Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.faqs.2",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["title"] = JsonSerializer.SerializeToElement(block.Title),
            ["description"] = JsonSerializer.SerializeToElement(block.Description),
            ["items"] = JsonSerializer.SerializeToElement(block.Items)
        }
    };

        /// <summary>
    /// FromNode method.
    /// </summary>
public static Faq2Block FromNode(NeoPageNode node) => new()
    {
        Title = GetString(node, "title", "FAQs"),
        Description = GetString(node, "description", ""),
        Items = node.Properties.TryGetValue("items", out var element) && element.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<AeroFaqItem>>(element.GetRawText()) ?? Faq2Block.DefaultItems.Select(CloneItem).ToList()
            : Faq2Block.DefaultItems.Select(CloneItem).ToList()
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;

    private static AeroFaqItem CloneItem(AeroFaqItem item) => new()
    {
        Question = item.Question,
        Answer = item.Answer
    };
}
