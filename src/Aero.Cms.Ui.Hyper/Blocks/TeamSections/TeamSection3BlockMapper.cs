using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.TeamSections;

/// <summary>
/// Represents a class for TeamSection3BlockMapper.
/// </summary>
public static class TeamSection3BlockMapper
{
        /// <summary>
    /// ToNode method.
    /// </summary>
public static NeoPageNode ToNode(TeamSection3Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.team-sections.3",
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
public static TeamSection3Block FromNode(NeoPageNode node) => new()
    {
        Title = GetString(node, "title", "Our Team"),
        Description = GetString(node, "description", "Meet the people behind our success."),
        Members = node.Properties.TryGetValue("members", out var element) && element.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<TeamMember3>>(element.GetRawText()) ?? TeamSection3Block.DefaultMembers.Select(CloneMember).ToList()
            : TeamSection3Block.DefaultMembers.Select(CloneMember).ToList()
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;

    private static TeamMember3 CloneMember(TeamMember3 m) => new()
    {
        Name = m.Name,
        Role = m.Role,
        AvatarUrl = m.AvatarUrl
    };
}
