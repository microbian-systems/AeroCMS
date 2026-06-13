using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Footers;

public static class Footer10BlockMapper
{
    public static NeoPageNode ToNode(Footer10Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.footers.10",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["description"] = JsonSerializer.SerializeToElement(block.Description),
            ["socialLinks"] = JsonSerializer.SerializeToElement(block.SocialLinks),
            ["linkColumns"] = JsonSerializer.SerializeToElement(block.LinkColumns),
            ["copyright"] = JsonSerializer.SerializeToElement(block.Copyright),
            ["legalLinks"] = JsonSerializer.SerializeToElement(block.LegalLinks)
        }
    };

    public static Footer10Block FromNode(NeoPageNode node) => new()
    {
        Description = GetString(node, "description", "Lorem ipsum dolor, sit amet consectetur adipisicing elit. Incidunt consequuntur amet culpa cum itaque neque."),
        Copyright = GetString(node, "copyright", "&copy; 2022 Company Name"),
        SocialLinks = node.Properties.TryGetValue("socialLinks", out var sl) && sl.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<FooterSocialLink>>(sl.GetRawText()) ?? FooterDefaults.DefaultSocialLinks.Select(FooterDefaults.CloneSocialLink).ToList()
            : FooterDefaults.DefaultSocialLinks.Select(FooterDefaults.CloneSocialLink).ToList(),
        LinkColumns = node.Properties.TryGetValue("linkColumns", out var lc) && lc.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<FooterLinkColumn>>(lc.GetRawText()) ?? Footer10Block.DefaultLinkColumns.Select(CloneColumn).ToList()
            : Footer10Block.DefaultLinkColumns.Select(CloneColumn).ToList(),
        LegalLinks = node.Properties.TryGetValue("legalLinks", out var ll) && ll.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<FooterLink>>(ll.GetRawText()) ?? Footer10Block.DefaultLegalLinks.Select(CloneLink).ToList()
            : Footer10Block.DefaultLegalLinks.Select(CloneLink).ToList()
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

    private static FooterLink CloneLink(FooterLink link) => new()
    {
        Text = link.Text,
        Url = link.Url
    };
}
