using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Footers;

public static class Footer3BlockMapper
{
    public static NeoPageNode ToNode(Footer3Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.footers.3",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["description"] = JsonSerializer.SerializeToElement(block.Description),
            ["socialLinks"] = JsonSerializer.SerializeToElement(block.SocialLinks),
            ["linkColumns"] = JsonSerializer.SerializeToElement(block.LinkColumns),
            ["copyright"] = JsonSerializer.SerializeToElement(block.Copyright)
        }
    };

    public static Footer3Block FromNode(NeoPageNode node) => new()
    {
        Description = GetString(node, "description", "Lorem ipsum dolor, sit amet consectetur adipisicing elit. Esse non cupiditate quae nam molestias."),
        SocialLinks = node.Properties.TryGetValue("socialLinks", out var sl) && sl.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<FooterSocialLink>>(sl.GetRawText()) ?? FooterDefaults.DefaultSocialLinks.Select(FooterDefaults.CloneSocialLink).ToList()
            : FooterDefaults.DefaultSocialLinks.Select(FooterDefaults.CloneSocialLink).ToList(),
        LinkColumns = node.Properties.TryGetValue("linkColumns", out var lc) && lc.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<FooterLinkColumn>>(lc.GetRawText()) ?? FooterDefaults.DefaultLinkColumns4.Select(FooterDefaults.CloneColumn).ToList()
            : FooterDefaults.DefaultLinkColumns4.Select(FooterDefaults.CloneColumn).ToList(),
        Copyright = GetString(node, "copyright", "&copy; 2022. Company Name. All rights reserved.")
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;
}
