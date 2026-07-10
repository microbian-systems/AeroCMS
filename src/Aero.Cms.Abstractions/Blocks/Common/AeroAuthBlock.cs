using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Abstractions.Blocks.Common;

/// <summary>
/// A Sign-In or Sign-Up block for lead generation or user auth portals.
/// </summary>
[BlockMetadata("aero_auth", "Aero Auth", Category = "Aero")]
public class AeroAuthBlock : BlockBase
{
        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => "aero_auth";

        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string? Title { get; set; }
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description { get; set; }
        /// <summary>
    /// Gets or sets the Form Action Url.
    /// </summary>
public string? FormActionUrl { get; set; }
        /// <summary>
    /// Gets or sets the Submit Button Text.
    /// </summary>
public string? SubmitButtonText { get; set; }
        /// <summary>
    /// Gets or sets the Alternative Link Text.
    /// </summary>
public string? AlternativeLinkText { get; set; }
        /// <summary>
    /// Gets or sets the Alternative Link Url.
    /// </summary>
public string? AlternativeLinkUrl { get; set; }
        /// <summary>
    /// Gets or sets the Background Image Url.
    /// </summary>
public string? BackgroundImageUrl { get; set; }
        /// <summary>
    /// Gets or sets the Layout.
    /// </summary>
public AeroAuthLayout Layout { get; set; } = AeroAuthLayout.Card;
        /// <summary>
    /// Gets or sets the Show Social Logins.
    /// </summary>
public bool ShowSocialLogins { get; set; } = false;

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}

/// <summary>
/// Defines an enumeration for AeroAuthLayout.
/// </summary>
public enum AeroAuthLayout
{
    Card,
    SideImage,
    Page,
    Centered
}
