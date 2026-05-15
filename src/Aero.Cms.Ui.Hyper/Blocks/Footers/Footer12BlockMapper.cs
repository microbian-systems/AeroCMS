using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Footers;

public static class Footer12BlockMapper
{
    public static NeoPageNode ToNode(Footer12Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.footers.12",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["ctaTitle"] = JsonSerializer.SerializeToElement(block.CtaTitle),
            ["ctaText"] = JsonSerializer.SerializeToElement(block.CtaText),
            ["ctaUrl"] = JsonSerializer.SerializeToElement(block.CtaUrl),
            ["description"] = JsonSerializer.SerializeToElement(block.Description),
            ["linkColumns"] = JsonSerializer.SerializeToElement(block.LinkColumns),
            ["socialLinks"] = JsonSerializer.SerializeToElement(block.SocialLinks),
            ["copyright"] = JsonSerializer.SerializeToElement(block.Copyright)
        }
    };

    public static Footer12Block FromNode(NeoPageNode node) => new()
    {
        CtaTitle = GetString(node, "ctaTitle", "Make Your Next Career Move!"),
        CtaText = GetString(node, "ctaText", "Let's Get Started"),
        CtaUrl = GetString(node, "ctaUrl", "#"),
        Description = GetString(node, "description", "CTA banner footer with link columns and social icons."),
        LinkColumns = node.Properties.TryGetValue("linkColumns", out var lc) && lc.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<FooterLinkColumn>>(lc.GetRawText()) ?? Footer12Block.DefaultLinkColumns.Select(CloneColumn).ToList()
            : Footer12Block.DefaultLinkColumns.Select(CloneColumn).ToList(),
        SocialLinks = node.Properties.TryGetValue("socialLinks", out var sl) && sl.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<FooterSocialLink>>(sl.GetRawText()) ?? FooterDefaults.DefaultSocialLinks.Select(FooterDefaults.CloneSocialLink).ToList()
            : FooterDefaults.DefaultSocialLinks.Select(FooterDefaults.CloneSocialLink).ToList(),
        Copyright = GetString(node, "copyright", "&copy; 2022. Company Name. All rights reserved.")
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
}
