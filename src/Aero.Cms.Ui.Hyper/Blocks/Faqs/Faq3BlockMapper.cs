using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Faqs;

public static class Faq3BlockMapper
{
    public static NeoPageNode ToNode(Faq3Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.faqs.3",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["title"] = JsonSerializer.SerializeToElement(block.Title),
            ["description"] = JsonSerializer.SerializeToElement(block.Description),
            ["items"] = JsonSerializer.SerializeToElement(block.Items)
        }
    };

    public static Faq3Block FromNode(NeoPageNode node) => new()
    {
        Title = GetString(node, "title", "FAQs"),
        Description = GetString(node, "description", ""),
        Items = node.Properties.TryGetValue("items", out var element) && element.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<AeroFaqItem>>(element.GetRawText()) ?? Faq3Block.DefaultItems.Select(CloneItem).ToList()
            : Faq3Block.DefaultItems.Select(CloneItem).ToList()
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
