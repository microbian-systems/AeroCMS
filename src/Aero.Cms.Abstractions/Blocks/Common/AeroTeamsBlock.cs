using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Abstractions.Blocks.Common;

/// <summary>
/// A team section for showcasing company members.
/// </summary>
[BlockMetadata("aero_teams", "Aero Teams", Category = "Aero")]
public class AeroTeamsBlock : BlockBase
{
        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => "aero_teams";

        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string? Title { get; set; }
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description { get; set; }
        /// <summary>
    /// Gets or sets the Members.
    /// </summary>
public List<AeroTeamMember> Members { get; set; } = new();
        /// <summary>
    /// Gets or sets the Aero Layout.
    /// </summary>
public string? AeroLayout { get; set; } = "Grid"; // Grid, List, Bordered

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}

/// <summary>
/// Represents a class for AeroTeamMember.
/// </summary>
public class AeroTeamMember
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public string? Name { get; set; }
        /// <summary>
    /// Gets or sets the Role.
    /// </summary>
public string? Role { get; set; }
        /// <summary>
    /// Gets or sets the Avatar Url.
    /// </summary>
public string? AvatarUrl { get; set; }
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description { get; set; }
        /// <summary>
    /// Gets or sets the Linked In Url.
    /// </summary>
public string? LinkedInUrl { get; set; }
        /// <summary>
    /// Gets or sets the Twitter Url.
    /// </summary>
public string? TwitterUrl { get; set; }
}
