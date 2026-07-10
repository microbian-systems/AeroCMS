using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.ContactForms;

/// <summary>
/// Represents a class for ContactForm5BlockMapper.
/// </summary>
public static class ContactForm5BlockMapper
{
        /// <summary>
    /// ToNode method.
    /// </summary>
public static NeoPageNode ToNode(ContactForm5Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.contact-forms.5",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["title"] = JsonSerializer.SerializeToElement(block.Title),
            ["description"] = JsonSerializer.SerializeToElement(block.Description),
            ["phoneLabel"] = JsonSerializer.SerializeToElement(block.PhoneLabel),
            ["emailLabel"] = JsonSerializer.SerializeToElement(block.EmailLabel),
            ["locationLabel"] = JsonSerializer.SerializeToElement(block.LocationLabel),
            ["nameLabel"] = JsonSerializer.SerializeToElement(block.NameLabel),
            ["namePlaceholder"] = JsonSerializer.SerializeToElement(block.NamePlaceholder),
            ["emailFieldLabel"] = JsonSerializer.SerializeToElement(block.EmailFieldLabel),
            ["emailPlaceholder"] = JsonSerializer.SerializeToElement(block.EmailPlaceholder),
            ["messageLabel"] = JsonSerializer.SerializeToElement(block.MessageLabel),
            ["messagePlaceholder"] = JsonSerializer.SerializeToElement(block.MessagePlaceholder),
            ["ctaText"] = JsonSerializer.SerializeToElement(block.CtaText),
            ["formAction"] = JsonSerializer.SerializeToElement(block.FormAction)
        }
    };

        /// <summary>
    /// FromNode method.
    /// </summary>
public static ContactForm5Block FromNode(NeoPageNode node) => new()
    {
        Title = GetString(node, "title", "Get in touch"),
        Description = GetString(node, "description", "Lorem, ipsum dolor sit amet consectetur adipisicing elit."),
        PhoneLabel = GetString(node, "phoneLabel", "+1 (555) 123-4567"),
        EmailLabel = GetString(node, "emailLabel", "info@example.com"),
        LocationLabel = GetString(node, "locationLabel", "123 Main St, Anytown, USA"),
        NameLabel = GetString(node, "nameLabel", "Name"),
        NamePlaceholder = GetString(node, "namePlaceholder", "Your name"),
        EmailFieldLabel = GetString(node, "emailFieldLabel", "Email"),
        EmailPlaceholder = GetString(node, "emailPlaceholder", "Your email"),
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
