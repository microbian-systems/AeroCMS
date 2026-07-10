using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Cards;

/// <summary>
/// Represents a class for Card6BlockMapper.
/// </summary>
public static class Card6BlockMapper
{
        /// <summary>
    /// ToNode method.
    /// </summary>
public static NeoPageNode ToNode(Card6Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.cards.6",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement(block.Name),
            ["avatarUrl"] = JsonSerializer.SerializeToElement(block.AvatarUrl),
            ["socialLinks"] = JsonSerializer.SerializeToElement(block.SocialLinks),
            ["projects"] = JsonSerializer.SerializeToElement(block.Projects)
        }
    };

        /// <summary>
    /// FromNode method.
    /// </summary>
public static Card6Block FromNode(NeoPageNode node) => new()
    {
        Name = GetString(node, "name", "Claire Mac"),
        AvatarUrl = GetString(node, "avatarUrl", "https://images.unsplash.com/photo-1614644147724-2d4785d69962?auto=format&fit=crop&q=80&w=1160"),
        SocialLinks = node.Properties.TryGetValue("socialLinks", out var slElement) && slElement.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<Card6SocialLink>>(slElement.GetRawText()) ?? Card6Block.DefaultSocialLinks.Select(CloneSocialLink).ToList()
            : Card6Block.DefaultSocialLinks.Select(CloneSocialLink).ToList(),
        Projects = node.Properties.TryGetValue("projects", out var pElement) && pElement.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<Card6Project>>(pElement.GetRawText()) ?? Card6Block.DefaultProjects.Select(CloneProject).ToList()
            : Card6Block.DefaultProjects.Select(CloneProject).ToList()
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;

    private static Card6SocialLink CloneSocialLink(Card6SocialLink link) => new()
    {
        Name = link.Name,
        Url = link.Url
    };

    private static Card6Project CloneProject(Card6Project project) => new()
    {
        Title = project.Title,
        Description = project.Description,
        Url = project.Url
    };
}
