using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.EmptyContent;

public static class EmptyContent1BlockMapper
{
    public static NeoPageNode ToNode(EmptyContent1Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.empty-content.1",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["title"] = JsonSerializer.SerializeToElement(block.Title),
            ["description"] = JsonSerializer.SerializeToElement(block.Description),
            ["ctaText"] = JsonSerializer.SerializeToElement(block.CtaText),
            ["ctaUrl"] = JsonSerializer.SerializeToElement(block.CtaUrl),
            ["ctaText2"] = JsonSerializer.SerializeToElement(block.CtaText2),
            ["ctaUrl2"] = JsonSerializer.SerializeToElement(block.CtaUrl2)
        }
    };

    public static EmptyContent1Block FromNode(NeoPageNode node) => new()
    {
        Title = GetString(node, "title", "Hmm, nothing found"),
        Description = GetString(node, "description", "We couldn't find what you were looking for."),
        CtaText = GetString(node, "ctaText", "Browse Popular Items"),
        CtaUrl = GetString(node, "ctaUrl", "#"),
        CtaText2 = GetString(node, "ctaText2", "Refine Search"),
        CtaUrl2 = GetString(node, "ctaUrl2", "#")
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;
}
