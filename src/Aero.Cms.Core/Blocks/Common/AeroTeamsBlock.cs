using Aero.Cms.Core.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Core.Blocks.Common;

/// <summary>
/// A team section for showcasing company members.
/// </summary>
[BlockMetadata("aero_teams", "Aero Teams", Category = "Aero")]
public class AeroTeamsBlock : BlockBase
{
    public override string BlockType => "aero_teams";

    public string? Title { get; set; }
    public string? Description { get; set; }
    public List<AeroTeamMember> Members { get; set; } = new();
    public string? AeroLayout { get; set; } = "Grid"; // Grid, List, Bordered

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}

public class AeroTeamMember
{
    public string? Name { get; set; }
    public string? Role { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Description { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? TwitterUrl { get; set; }
}
