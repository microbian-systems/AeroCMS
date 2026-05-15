using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Footers;

public static class Footer1BlockMapper
{
    public static NeoPageNode ToNode(Footer1Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.footers.1",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["newsletterTitle"] = JsonSerializer.SerializeToElement(block.NewsletterTitle),
            ["newsletterDescription"] = JsonSerializer.SerializeToElement(block.NewsletterDescription),
            ["emailPlaceholder"] = JsonSerializer.SerializeToElement(block.EmailPlaceholder),
            ["buttonText"] = JsonSerializer.SerializeToElement(block.ButtonText),
            ["linkColumns"] = JsonSerializer.SerializeToElement(block.LinkColumns),
            ["socialLinks"] = JsonSerializer.SerializeToElement(block.SocialLinks),
            ["copyright"] = JsonSerializer.SerializeToElement(block.Copyright),
            ["bottomLinks"] = JsonSerializer.SerializeToElement(block.BottomLinks)
        }
    };

    public static Footer1Block FromNode(NeoPageNode node) => new()
    {
        NewsletterTitle = GetString(node, "newsletterTitle", "Get the latest news!"),
        NewsletterDescription = GetString(node, "newsletterDescription", "Lorem ipsum dolor, sit amet consectetur adipisicing elit. Esse non cupiditate quae nam molestias."),
        EmailPlaceholder = GetString(node, "emailPlaceholder", "john@rhcp.com"),
        ButtonText = GetString(node, "buttonText", "Sign Up"),
        LinkColumns = node.Properties.TryGetValue("linkColumns", out var lc) && lc.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<FooterLinkColumn>>(lc.GetRawText()) ?? Footer1Block.DefaultLinkColumns.Select(CloneColumn).ToList()
            : Footer1Block.DefaultLinkColumns.Select(CloneColumn).ToList(),
        SocialLinks = node.Properties.TryGetValue("socialLinks", out var sl) && sl.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<FooterSocialLink>>(sl.GetRawText()) ?? Footer1Block.DefaultSocialLinks.Select(CloneSocialLink).ToList()
            : Footer1Block.DefaultSocialLinks.Select(CloneSocialLink).ToList(),
        Copyright = GetString(node, "copyright", "&copy; 2022. Company Name. All rights reserved."),
        BottomLinks = node.Properties.TryGetValue("bottomLinks", out var bl) && bl.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<FooterLink>>(bl.GetRawText()) ?? Footer1Block.DefaultBottomLinks.Select(CloneLink).ToList()
            : Footer1Block.DefaultBottomLinks.Select(CloneLink).ToList()
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;

    private static FooterLinkColumn CloneColumn(FooterLinkColumn col) => new()
    {
        Title = col.Title,
        Links = col.Links.Select(l => new FooterLink { Text = l.Text, Url = l.Url }).ToList()
    };

    private static FooterSocialLink CloneSocialLink(FooterSocialLink link) => new()
    {
        Name = link.Name,
        Url = link.Url
    };

    private static FooterLink CloneLink(FooterLink link) => new()
    {
        Text = link.Text,
        Url = link.Url
    };
}
