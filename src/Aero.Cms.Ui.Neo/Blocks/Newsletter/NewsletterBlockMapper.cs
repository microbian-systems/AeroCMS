using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Neo.Blocks.Newsletter;

public static class NewsletterBlockMapper
{
    public static NeoPageNode ToNode(NewsletterBlock block) => new()
    {
        NodeId = string.Empty,
        CatalogId = NewsletterBlock.BlockTypeId,
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["title"]       = JsonSerializer.SerializeToElement(block.Title),
            ["description"] = JsonSerializer.SerializeToElement(block.Description),
            ["placeholder"] = JsonSerializer.SerializeToElement(block.Placeholder),
            ["buttonText"]  = JsonSerializer.SerializeToElement(block.ButtonText),
            ["privacyText"] = JsonSerializer.SerializeToElement(block.PrivacyText),
        }
    };

    public static NewsletterBlock FromNode(NeoPageNode node) => new()
    {
        Title       = GetString(node, "title",       "Stay in the loop"),
        Description = GetString(node, "description",  string.Empty),
        Placeholder = GetString(node, "placeholder",  "Enter your email"),
        ButtonText  = GetString(node, "buttonText",   "Subscribe"),
        PrivacyText = GetString(node, "privacyText",  "We respect your privacy. Unsubscribe at any time."),
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;
}
