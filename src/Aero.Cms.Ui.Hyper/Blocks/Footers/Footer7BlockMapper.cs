using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Footers;

public static class Footer7BlockMapper
{
    public static NeoPageNode ToNode(Footer7Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.footers.7",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["newsletterTitle"] = JsonSerializer.SerializeToElement(block.NewsletterTitle),
            ["newsletterDescription"] = JsonSerializer.SerializeToElement(block.NewsletterDescription),
            ["emailPlaceholder"] = JsonSerializer.SerializeToElement(block.EmailPlaceholder),
            ["buttonText"] = JsonSerializer.SerializeToElement(block.ButtonText),
            ["socialLinks"] = JsonSerializer.SerializeToElement(block.SocialLinks),
            ["servicesLinks"] = JsonSerializer.SerializeToElement(block.ServicesLinks),
            ["aboutLinks"] = JsonSerializer.SerializeToElement(block.AboutLinks),
            ["supportLinks"] = JsonSerializer.SerializeToElement(block.SupportLinks),
            ["copyrightText"] = JsonSerializer.SerializeToElement(block.CopyrightText),
            ["createdWithText"] = JsonSerializer.SerializeToElement(block.CreatedWithText)
        }
    };

    public static Footer7Block FromNode(NeoPageNode node) => new()
    {
        NewsletterTitle = GetString(node, "newsletterTitle", "Want us to email you with the latest blockbuster news?"),
        NewsletterDescription = GetString(node, "newsletterDescription", "Lorem ipsum, dolor sit amet consectetur adipisicing elit. Praesentium natus quod eveniet aut perferendis distinctio iusto repudiandae, provident velit earum?"),
        EmailPlaceholder = GetString(node, "emailPlaceholder", "john@doe.com"),
        ButtonText = GetString(node, "buttonText", "Subscribe"),
        SocialLinks = GetList<FooterSocialLink>(node, "socialLinks") ?? FooterDefaults.DefaultSocialLinks.Select(CloneSocialLink).ToList(),
        ServicesLinks = GetList<FooterLink>(node, "servicesLinks") ?? Footer7Block.DefaultServicesLinks.Select(CloneLink).ToList(),
        AboutLinks = GetList<FooterLink>(node, "aboutLinks") ?? Footer7Block.DefaultAboutLinks.Select(CloneLink).ToList(),
        SupportLinks = GetList<FooterLink>(node, "supportLinks") ?? Footer7Block.DefaultSupportLinks.Select(CloneLink).ToList(),
        CopyrightText = GetString(node, "copyrightText", "&copy; Company 2022. All rights reserved."),
        CreatedWithText = GetString(node, "createdWithText", "Created with Laravel and Laravel Livewire.")
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;

    private static List<T>? GetList<T>(NeoPageNode node, string key) =>
        node.Properties.TryGetValue(key, out var element) && element.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<T>>(element.GetRawText())
            : null;

    private static FooterLink CloneLink(FooterLink link) => new()
    {
        Text = link.Text,
        Url = link.Url
    };

    private static FooterSocialLink CloneSocialLink(FooterSocialLink link) => new()
    {
        Name = link.Name,
        Url = link.Url
    };
}
