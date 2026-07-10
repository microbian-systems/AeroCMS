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
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.team-sections.2";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string Title { get; set; } = "Our Team";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string Description { get; set; } = "Meet the people behind our success.";
        /// <summary>
    /// Gets or sets the Members.
    /// </summary>
public List<TeamMember2> Members { get; set; } = DefaultMembers.Select(CloneMember).ToList();

        /// <summary>
    /// DefaultMembers.
    /// </summary>
public static readonly List<TeamMember2> DefaultMembers =
    [
        new() { Name = "Eric Johnson", Role = "Product Designer", AvatarUrl = "https://images.unsplash.com/photo-1633332755192-727a05c4013d?auto=format&fit=crop&q=80&w=1160", Description = "Lorem ipsum dolor sit amet consectetur adipisicing elit. Impedit, placeat facere? Iste nostrum odio magnam?", LinkedInUrl = "#" },
        new() { Name = "Jane Doe", Role = "Software Engineer", AvatarUrl = "https://images.unsplash.com/photo-1494790108377-be9c29b29330?auto=format&fit=crop&q=80&w=1160", Description = "Lorem ipsum dolor sit amet consectetur adipisicing elit. Impedit, placeat facere? Iste nostrum odio magnam?", LinkedInUrl = "#" },
        new() { Name = "Mike Smith", Role = "Marketing Lead", AvatarUrl = "https://images.unsplash.com/photo-1472099645785-5658abf4ff4e?auto=format&fit=crop&q=80&w=1160", Description = "Lorem ipsum dolor sit amet consectetur adipisicing elit. Impedit, placeat facere? Iste nostrum odio magnam?", LinkedInUrl = "#" }
    ];

        /// <summary>
    /// Accept method.
    /// </summary>
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

/// <summary>
/// Represents a class for TeamMember2.
/// </summary>
public sealed class TeamMember2
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public string Name { get; set; } = "";
        /// <summary>
    /// Gets or sets the Role.
    /// </summary>
public string Role { get; set; } = "";
        /// <summary>
    /// Gets or sets the Avatar Url.
    /// </summary>
public string AvatarUrl { get; set; } = "";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string Description { get; set; } = "";
        /// <summary>
    /// Gets or sets the Linked In Url.
    /// </summary>
public string LinkedInUrl { get; set; } = "#";
}
