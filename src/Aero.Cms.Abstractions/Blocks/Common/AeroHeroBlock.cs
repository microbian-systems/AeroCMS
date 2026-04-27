using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Abstractions.Blocks.Common;

/// <summary>
/// A high-level Hero block based on Meraki UI components.
/// </summary>
[BlockMetadata("aero_hero", "Aero Hero", Category = "Aero")]
public class AeroHeroBlock : BlockBase
{
    public override string BlockType => "aero_hero";

    public string? Title { get; set; }

    public string? Description { get; set; }

    public string? BackgroundImage { get; set; }
 
    public long? ImageId { get; set; }

    public AeroHeroLayout Layout { get; set; } = AeroHeroLayout.SideImage;

    public List<AeroButton>? Buttons { get; set; } = new();

    public bool FullWidth { get; set; } = false;

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}

public enum AeroHeroLayout
{
    SideImage,
    CenterContent,
    BackgroundImage,
    SideImageReversed
}

public class AeroButton
{
    public string? Text { get; set; }
    public string? Url { get; set; }
    public AeroButtonStyle Style { get; set; } = AeroButtonStyle.Primary;
}

public enum AeroButtonStyle
{
    Primary,
    Secondary,
    Ghost
}
