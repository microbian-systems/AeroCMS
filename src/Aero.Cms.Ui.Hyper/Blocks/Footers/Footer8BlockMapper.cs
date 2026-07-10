using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Footers;

/// <summary>
/// Represents a class for Footer8BlockMapper.
/// </summary>
public static class Footer8BlockMapper
{
        /// <summary>
    /// ToNode method.
    /// </summary>
public static NeoPageNode ToNode(Footer8Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.footers.8",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["description"] = JsonSerializer.SerializeToElement(block.Description),
            ["navLinks"] = JsonSerializer.SerializeToElement(block.NavLinks),
            ["socialLinks"] = JsonSerializer.SerializeToElement(block.SocialLinks)
        }
    };

        /// <summary>
    /// FromNode method.
    /// </summary>
public static Footer8Block FromNode(NeoPageNode node) => new()
    {
        Description = GetString(node, "description", "Lorem ipsum dolor, sit amet consectetur adipisicing elit. Incidunt consequuntur amet culpa cum itaque neque."),
        NavLinks = GetList<FooterLink>(node, "navLinks") ?? Footer8Block.DefaultNavLinks.Select(CloneLink).ToList(),
        SocialLinks = GetList<FooterSocialLink>(node, "socialLinks") ?? FooterDefaults.DefaultSocialLinks.Select(CloneSocialLink).ToList()
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
