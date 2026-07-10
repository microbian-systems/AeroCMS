using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.NewsletterSignup;

/// <summary>
/// Represents a class for NewsletterSignup2BlockMapper.
/// </summary>
public static class NewsletterSignup2BlockMapper
{
        /// <summary>
    /// ToNode method.
    /// </summary>
public static NeoPageNode ToNode(NewsletterSignup2Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.newsletter-signup.2",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["title"] = JsonSerializer.SerializeToElement(block.Title),
            ["description"] = JsonSerializer.SerializeToElement(block.Description),
            ["placeholder"] = JsonSerializer.SerializeToElement(block.Placeholder),
            ["ctaText"] = JsonSerializer.SerializeToElement(block.CtaText),
            ["formAction"] = JsonSerializer.SerializeToElement(block.FormAction)
        }
    };

        /// <summary>
    /// FromNode method.
    /// </summary>
public static NewsletterSignup2Block FromNode(NeoPageNode node) => new()
    {
        Title = GetString(node, "title", "Sign up for our newsletter"),
        Description = GetString(node, "description", "Lorem ipsum dolor sit amet consectetur adipisicing elit."),
        Placeholder = GetString(node, "placeholder", "Enter your email"),
        CtaText = GetString(node, "ctaText", "Sign Up"),
        FormAction = GetString(node, "formAction", "#")
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;
}
