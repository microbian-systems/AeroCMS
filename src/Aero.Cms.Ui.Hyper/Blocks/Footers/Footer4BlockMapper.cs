using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Footers;

/// <summary>
/// Represents a class for Footer4BlockMapper.
/// </summary>
public static class Footer4BlockMapper
{
        /// <summary>
    /// ToNode method.
    /// </summary>
public static NeoPageNode ToNode(Footer4Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.footers.4",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["title"] = JsonSerializer.SerializeToElement(block.Title),
            ["description"] = JsonSerializer.SerializeToElement(block.Description),
            ["ctaText"] = JsonSerializer.SerializeToElement(block.CtaText),
            ["ctaUrl"] = JsonSerializer.SerializeToElement(block.CtaUrl),
            ["bottomLinks"] = JsonSerializer.SerializeToElement(block.BottomLinks),
            ["socialLinks"] = JsonSerializer.SerializeToElement(block.SocialLinks)
        }
    };

        /// <summary>
    /// FromNode method.
    /// </summary>
public static Footer4Block FromNode(NeoPageNode node) => new()
    {
        Title = GetString(node, "title", "Customise Your Product"),
        Description = GetString(node, "description", "Lorem ipsum dolor, sit amet consectetur adipisicing elit. Cum maiores ipsum eos temporibus ea nihil."),
        CtaText = GetString(node, "ctaText", "Get Started"),
        CtaUrl = GetString(node, "ctaUrl", "#"),
        BottomLinks = node.Properties.TryGetValue("bottomLinks", out var bl) && bl.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<FooterLink>>(bl.GetRawText()) ?? Footer4Block.DefaultBottomLinks.Select(CloneLink).ToList()
            : Footer4Block.DefaultBottomLinks.Select(CloneLink).ToList(),
        SocialLinks = node.Properties.TryGetValue("socialLinks", out var sl) && sl.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<FooterSocialLink>>(sl.GetRawText()) ?? FooterDefaults.DefaultSocialLinks.Select(FooterDefaults.CloneSocialLink).ToList()
            : FooterDefaults.DefaultSocialLinks.Select(FooterDefaults.CloneSocialLink).ToList()
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;

    private static FooterLink CloneLink(FooterLink link) => new()
    {
        Text = link.Text,
        Url = link.Url
    };
}
