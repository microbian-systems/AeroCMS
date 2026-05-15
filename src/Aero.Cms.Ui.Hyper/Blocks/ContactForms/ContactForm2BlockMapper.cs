using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.ContactForms;

public static class ContactForm2BlockMapper
{
    public static NeoPageNode ToNode(ContactForm2Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.contact-forms.2",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["nameLabel"] = JsonSerializer.SerializeToElement(block.NameLabel),
            ["namePlaceholder"] = JsonSerializer.SerializeToElement(block.NamePlaceholder),
            ["emailLabel"] = JsonSerializer.SerializeToElement(block.EmailLabel),
            ["emailPlaceholder"] = JsonSerializer.SerializeToElement(block.EmailPlaceholder),
            ["subjectLabel"] = JsonSerializer.SerializeToElement(block.SubjectLabel),
            ["subjectDefaultOption"] = JsonSerializer.SerializeToElement(block.SubjectDefaultOption),
            ["priorityLabel"] = JsonSerializer.SerializeToElement(block.PriorityLabel),
            ["priorityDefaultOption"] = JsonSerializer.SerializeToElement(block.PriorityDefaultOption),
            ["messageLabel"] = JsonSerializer.SerializeToElement(block.MessageLabel),
            ["messagePlaceholder"] = JsonSerializer.SerializeToElement(block.MessagePlaceholder),
            ["ctaText"] = JsonSerializer.SerializeToElement(block.CtaText),
            ["formAction"] = JsonSerializer.SerializeToElement(block.FormAction)
        }
    };

    public static ContactForm2Block FromNode(NeoPageNode node) => new()
    {
        NameLabel = GetString(node, "nameLabel", "Name"),
        NamePlaceholder = GetString(node, "namePlaceholder", "Your name"),
        EmailLabel = GetString(node, "emailLabel", "Email"),
        EmailPlaceholder = GetString(node, "emailPlaceholder", "Your email"),
        SubjectLabel = GetString(node, "subjectLabel", "Subject"),
        SubjectDefaultOption = GetString(node, "subjectDefaultOption", "Select a subject"),
        PriorityLabel = GetString(node, "priorityLabel", "Priority"),
        PriorityDefaultOption = GetString(node, "priorityDefaultOption", "Select a priority"),
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
