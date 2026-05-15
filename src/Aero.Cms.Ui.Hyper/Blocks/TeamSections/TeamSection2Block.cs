using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.TeamSections;

/// <summary>
/// HyperUI Team Sections 2 — three-column grid with image, name, role, LinkedIn icon, and description.
/// Source: hyperui/public/examples/marketing/team-sections/2.html + 2-dark.html.
/// </summary>
[BlockMetadata(
    "hyper.team-sections.2",
    "Team Sections 2",
    Category = "Hyper",
    Icon = "users",
    SortOrder = 112,
    SchemaVersion = 1)]
public sealed class TeamSection2Block : BlockBase
{
    public const string BlockTypeId = "hyper.team-sections.2";

    public override string BlockType => BlockTypeId;

    public string Title { get; set; } = "Our Team";
    public string Description { get; set; } = "Meet the people behind our success.";
    public List<TeamMember2> Members { get; set; } = DefaultMembers.Select(CloneMember).ToList();

    public static readonly List<TeamMember2> DefaultMembers =
    [
        new() { Name = "Eric Johnson", Role = "Product Designer", AvatarUrl = "https://images.unsplash.com/photo-1633332755192-727a05c4013d?auto=format&fit=crop&q=80&w=1160", Description = "Lorem ipsum dolor sit amet consectetur adipisicing elit. Impedit, placeat facere? Iste nostrum odio magnam?", LinkedInUrl = "#" },
        new() { Name = "Jane Doe", Role = "Software Engineer", AvatarUrl = "https://images.unsplash.com/photo-1494790108377-be9c29b29330?auto=format&fit=crop&q=80&w=1160", Description = "Lorem ipsum dolor sit amet consectetur adipisicing elit. Impedit, placeat facere? Iste nostrum odio magnam?", LinkedInUrl = "#" },
        new() { Name = "Mike Smith", Role = "Marketing Lead", AvatarUrl = "https://images.unsplash.com/photo-1472099645785-5658abf4ff4e?auto=format&fit=crop&q=80&w=1160", Description = "Lorem ipsum dolor sit amet consectetur adipisicing elit. Impedit, placeat facere? Iste nostrum odio magnam?", LinkedInUrl = "#" }
    ];

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);

    private static TeamMember2 CloneMember(TeamMember2 m) => new()
    {
        Name = m.Name,
        Role = m.Role,
        AvatarUrl = m.AvatarUrl,
        Description = m.Description,
        LinkedInUrl = m.LinkedInUrl
    };
}

public sealed class TeamMember2
{
    public string Name { get; set; } = "";
    public string Role { get; set; } = "";
    public string AvatarUrl { get; set; } = "";
    public string Description { get; set; } = "";
    public string LinkedInUrl { get; set; } = "#";
}
