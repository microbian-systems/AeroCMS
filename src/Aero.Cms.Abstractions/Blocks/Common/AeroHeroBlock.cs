using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Abstractions.Blocks.Common;

/// <summary>
/// A high-level Hero block based on Meraki UI components.
/// </summary>
[BlockMetadata("aero_hero", "Aero Hero", Category = "Aero")]
public class AeroHeroBlock : BlockBase
{
        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => "aero_hero";

        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string? Title { get; set; }

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description { get; set; }

        /// <summary>
    /// Gets or sets the Background Image.
    /// </summary>
public string? BackgroundImage { get; set; }
 
        /// <summary>
    /// Gets or sets the Image Id.
    /// </summary>
public long? ImageId { get; set; }

        /// <summary>
    /// Gets or sets the Layout.
    /// </summary>
public AeroHeroLayout Layout { get; set; } = AeroHeroLayout.SideImage;

        /// <summary>
    /// Gets or sets the Buttons.
    /// </summary>
public List<AeroButton>? Buttons { get; set; } = new();

        /// <summary>
    /// Gets or sets the Full Width.
    /// </summary>
public bool FullWidth { get; set; } = false;

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}

/// <summary>
/// Defines an enumeration for AeroHeroLayout.
/// </summary>
public enum AeroHeroLayout
{
    SideImage,
    CenterContent,
    BackgroundImage,
    SideImageReversed
}

/// <summary>
/// Represents a class for AeroButton.
/// </summary>
public class AeroButton
{
        /// <summary>
    /// Gets or sets the Text.
    /// </summary>
public string? Text { get; set; }
        /// <summary>
    /// Gets or sets the Url.
    /// </summary>
public string? Url { get; set; }
        /// <summary>
    /// Gets or sets the Style.
    /// </summary>
public AeroButtonStyle Style { get; set; } = AeroButtonStyle.Primary;
}

/// <summary>
/// Defines an enumeration for AeroButtonStyle.
/// </summary>
public enum AeroButtonStyle
{
    Primary,
    Secondary,
    Ghost
}
