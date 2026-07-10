using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.TeamSections;

/// <summary>
/// Represents a class for TeamSection2BlockMapper.
/// </summary>
public static class TeamSection2BlockMapper
{
        /// <summary>
    /// ToNode method.
    /// </summary>
public static NeoPageNode ToNode(TeamSection2Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.team-sections.2",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["title"] = JsonSerializer.SerializeToElement(block.Title),
            ["description"] = JsonSerializer.SerializeToElement(block.Description),
            ["members"] = JsonSerializer.SerializeToElement(block.Members)
        }
    };

        /// <summary>
    /// FromNode method.
    /// </summary>
public static TeamSection2Block FromNode(NeoPageNode node) => new()
    {
        Title = GetString(node, "title", "Our Team"),
        Description = GetString(node, "description", "Meet the people behind our success."),
        Members = node.Properties.TryGetValue("members", out var element) && element.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<TeamMember2>>(element.GetRawText()) ?? TeamSection2Block.DefaultMembers.Select(CloneMember).ToList()
            : TeamSection2Block.DefaultMembers.Select(CloneMember).ToList()
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;

    private static TeamMember2 CloneMember(TeamMember2 m) => new()
    {
        Name = m.Name,
        Role = m.Role,
        AvatarUrl = m.AvatarUrl,
        Description = m.Description,
        LinkedInUrl = m.LinkedInUrl
    };
}
