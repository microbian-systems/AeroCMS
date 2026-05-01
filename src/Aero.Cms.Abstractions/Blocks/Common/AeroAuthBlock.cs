using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Abstractions.Blocks.Common;

/// <summary>
/// A Sign-In or Sign-Up block for lead generation or user auth portals.
/// </summary>
[BlockMetadata("aero_auth", "Aero Auth", Category = "Aero")]
public class AeroAuthBlock : BlockBase
{
    public override string BlockType => "aero_auth";

    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? FormActionUrl { get; set; }
    public string? SubmitButtonText { get; set; }
    public string? AlternativeLinkText { get; set; }
    public string? AlternativeLinkUrl { get; set; }
    public string? BackgroundImageUrl { get; set; }
    public AeroAuthLayout Layout { get; set; } = AeroAuthLayout.Card;
    public bool ShowSocialLogins { get; set; } = false;

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}

public enum AeroAuthLayout
{
    Card,
    SideImage,
    Page,
    Centered
}
