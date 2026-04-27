using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Abstractions.Blocks.Common;

/// <summary>
/// A portfolio showcase block for displaying project cards or visual galleries.
/// </summary>
[BlockMetadata("aero_portfolio", "Aero Portfolio", Category = "Aero")]
public class AeroPortfolioBlock : BlockBase
{
    public override string BlockType => "aero_portfolio";

    public string? Title { get; set; }
    public string? Description { get; set; }
    public List<AeroPortfolioItem> Items { get; set; } = new();
    public AeroPortfolioLayout Layout { get; set; } = AeroPortfolioLayout.Cards;

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}

public class AeroPortfolioItem
{
    public string? ProjectTitle { get; set; }
    public string? ProjectCategory { get; set; }
    public string? ProjectImageUrl { get; set; }
    public string? ProjectUrl { get; set; }
    public string? ProjectDescription { get; set; }
}

public enum AeroPortfolioLayout
{
    Cards,
    Centered,
    HoverEffect,
    Filter
}
