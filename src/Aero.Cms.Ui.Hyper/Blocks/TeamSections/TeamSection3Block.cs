using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.TeamSections;

/// <summary>
/// HyperUI Team Sections 3 — 6-column grid with circular images, name, and role.
/// Source: hyperui/public/examples/marketing/team-sections/3.html + 3-dark.html.
/// </summary>
[BlockMetadata(
    "hyper.team-sections.3",
    "Team Sections 3",
    Category = "Hyper",
    Icon = "users",
    SortOrder = 113,
    SchemaVersion = 1)]
public sealed class TeamSection3Block : BlockBase
{
    public const string BlockTypeId = "hyper.team-sections.3";

    public override string BlockType => BlockTypeId;

    public string Title { get; set; } = "Our Team";
    public string Description { get; set; } = "Meet the people behind our success.";
    public List<TeamMember3> Members { get; set; } = DefaultMembers.Select(CloneMember).ToList();

    public static readonly List<TeamMember3> DefaultMembers =
    [
        new() { Name = "Eric Johnson", Role = "Product Designer", AvatarUrl = "https://images.unsplash.com/photo-1633332755192-727a05c4013d?auto=format&fit=crop&q=80&w=1160" },
        new() { Name = "Jane Doe", Role = "Software Engineer", AvatarUrl = "https://images.unsplash.com/photo-1494790108377-be9c29b29330?auto=format&fit=crop&q=80&w=1160" },
        new() { Name = "Mike Smith", Role = "Marketing Lead", AvatarUrl = "https://images.unsplash.com/photo-1472099645785-5658abf4ff4e?auto=format&fit=crop&q=80&w=1160" },
        new() { Name = "Sarah Lee", Role = "Designer", AvatarUrl = "https://images.unsplash.com/photo-1438761681033-6461ffad8d80?auto=format&fit=crop&q=80&w=1160" },
        new() { Name = "Tom Brown", Role = "Developer", AvatarUrl = "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?auto=format&fit=crop&q=80&w=1160" },
        new() { Name = "Lisa Wang", Role = "Product Manager", AvatarUrl = "https://images.unsplash.com/photo-1534528741775-53994a69daeb?auto=format&fit=crop&q=80&w=1160" }
    ];

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);

    private static TeamMember3 CloneMember(TeamMember3 m) => new()
    {
        Name = m.Name,
        Role = m.Role,
        AvatarUrl = m.AvatarUrl
    };
}

public sealed class TeamMember3
{
    public string Name { get; set; } = "";
    public string Role { get; set; } = "";
    public string AvatarUrl { get; set; } = "";
}
