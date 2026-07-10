using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Abstractions.Blocks.Common;

/// <summary>
/// A Call-To-Action (CTA) block for driving user conversions.
/// </summary>
[BlockMetadata("aero_cta", "Aero CTA", Category = "Aero")]
public class AeroCtaBlock : BlockBase
{
        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => "aero_cta";

        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string? Title { get; set; }
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description { get; set; }
        /// <summary>
    /// Gets or sets the Cta Text.
    /// </summary>
public string? CtaText { get; set; }
        /// <summary>
    /// Gets or sets the Cta Url.
    /// </summary>
public string? CtaUrl { get; set; }
        /// <summary>
    /// Gets or sets the Image Url.
    /// </summary>
public string? ImageUrl { get; set; }
        /// <summary>
    /// Gets or sets the Layout.
    /// </summary>
public AeroCtaLayout Layout { get; set; } = AeroCtaLayout.Card;

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}

/// <summary>
/// Defines an enumeration for AeroCtaLayout.
/// </summary>
public enum AeroCtaLayout
{
    Simple,
    Card,
    Split
}
