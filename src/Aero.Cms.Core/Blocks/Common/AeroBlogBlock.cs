using Aero.Cms.Core.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Core.Blocks.Common;

/// <summary>
/// A blog post grid block for displaying latest stories.
/// </summary>
[BlockMetadata("aero_blog", "Aero Blog", Category = "Aero")]
public class AeroBlogBlock : BlockBase
{
    public override string BlockType => "aero_blog";

    public string? Title { get; set; }
    public string? Description { get; set; }
    public List<AeroBlogItem> Posts { get; set; } = new();
    public string? AeroLayout { get; set; } = "Cards"; // Cards, List, Large

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}

public class AeroBlogItem
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public string? AuthorName { get; set; }
    public string? PublishedAt { get; set; }
    public string? Category { get; set; }
    public string? PostUrl { get; set; }
}
