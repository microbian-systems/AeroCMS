using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.TeamSections;

public static class TeamSection1BlockMapper
{
    public static NeoPageNode ToNode(TeamSection1Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.team-sections.1",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["title"] = JsonSerializer.SerializeToElement(block.Title),
            ["description"] = JsonSerializer.SerializeToElement(block.Description),
            ["members"] = JsonSerializer.SerializeToElement(block.Members)
        }
    };

    public static TeamSection1Block FromNode(NeoPageNode node) => new()
    {
        Title = GetString(node, "title", "Our Team"),
        Description = GetString(node, "description", "Meet the people behind our success."),
        Members = node.Properties.TryGetValue("members", out var element) && element.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<TeamMember1>>(element.GetRawText()) ?? TeamSection1Block.DefaultMembers.Select(CloneMember).ToList()
            : TeamSection1Block.DefaultMembers.Select(CloneMember).ToList()
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;

    private static TeamMember1 CloneMember(TeamMember1 m) => new()
    {
        Name = m.Name,
        Role = m.Role,
        AvatarUrl = m.AvatarUrl,
        LinkedInUrl = m.LinkedInUrl
    };
}
