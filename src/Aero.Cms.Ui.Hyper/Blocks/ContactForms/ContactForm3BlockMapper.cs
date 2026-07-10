using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.ContactForms;

/// <summary>
/// Represents a class for ContactForm3BlockMapper.
/// </summary>
public static class ContactForm3BlockMapper
{
        /// <summary>
    /// ToNode method.
    /// </summary>
public static NeoPageNode ToNode(ContactForm3Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.contact-forms.3",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["nameLabel"] = JsonSerializer.SerializeToElement(block.NameLabel),
            ["namePlaceholder"] = JsonSerializer.SerializeToElement(block.NamePlaceholder),
            ["emailLabel"] = JsonSerializer.SerializeToElement(block.EmailLabel),
            ["emailPlaceholder"] = JsonSerializer.SerializeToElement(block.EmailPlaceholder),
            ["inquiryLegend"] = JsonSerializer.SerializeToElement(block.InquiryLegend),
            ["messageLabel"] = JsonSerializer.SerializeToElement(block.MessageLabel),
            ["messagePlaceholder"] = JsonSerializer.SerializeToElement(block.MessagePlaceholder),
            ["ctaText"] = JsonSerializer.SerializeToElement(block.CtaText),
            ["formAction"] = JsonSerializer.SerializeToElement(block.FormAction)
        }
    };

        /// <summary>
    /// FromNode method.
    /// </summary>
public static ContactForm3Block FromNode(NeoPageNode node) => new()
    {
        NameLabel = GetString(node, "nameLabel", "Name"),
        NamePlaceholder = GetString(node, "namePlaceholder", "Your name"),
        EmailLabel = GetString(node, "emailLabel", "Email"),
        EmailPlaceholder = GetString(node, "emailPlaceholder", "Your email"),
        InquiryLegend = GetString(node, "inquiryLegend", "Inquiry"),
        MessageLabel = GetString(node, "messageLabel", "Message"),
        MessagePlaceholder = GetString(node, "messagePlaceholder", "Your message"),
        CtaText = GetString(node, "ctaText", "Send Message"),
        FormAction = GetString(node, "formAction", "#")
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;
}
