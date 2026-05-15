using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.TeamSections;

/// <summary>
/// HyperUI Team Sections 1 — three-column grid with image, name, role, and LinkedIn icon.
/// Source: hyperui/public/examples/marketing/team-sections/1.html + 1-dark.html.
/// </summary>
[BlockMetadata(
    "hyper.team-sections.1",
    "Team Sections 1",
    Category = "Hyper",
    Icon = "users",
    SortOrder = 111,
    SchemaVersion = 1)]
public sealed class TeamSection1Block : BlockBase
{
    public const string BlockTypeId = "hyper.team-sections.1";

    public override string BlockType => BlockTypeId;

    public string Title { get; set; } = "Our Team";
    public string Description { get; set; } = "Meet the people behind our success.";
    public List<TeamMember1> Members { get; set; } = DefaultMembers.Select(CloneMember).ToList();

    public static readonly List<TeamMember1> DefaultMembers =
    [
        new() { Name = "Eric Johnson", Role = "Product Designer", AvatarUrl = "https://images.unsplash.com/photo-1633332755192-727a05c4013d?auto=format&fit=crop&q=80&w=1160", LinkedInUrl = "#" },
        new() { Name = "Jane Doe", Role = "Software Engineer", AvatarUrl = "https://images.unsplash.com/photo-1494790108377-be9c29b29330?auto=format&fit=crop&q=80&w=1160", LinkedInUrl = "#" },
        new() { Name = "Mike Smith", Role = "Marketing Lead", AvatarUrl = "https://images.unsplash.com/photo-1472099645785-5658abf4ff4e?auto=format&fit=crop&q=80&w=1160", LinkedInUrl = "#" }
    ];

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);

    private static TeamMember1 CloneMember(TeamMember1 m) => new()
    {
        Name = m.Name,
        Role = m.Role,
        AvatarUrl = m.AvatarUrl,
        LinkedInUrl = m.LinkedInUrl
    };
}

public sealed class TeamMember1
{
    public string Name { get; set; } = "";
    public string Role { get; set; } = "";
    public string AvatarUrl { get; set; } = "";
    public string LinkedInUrl { get; set; } = "#";
}
