using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Abstractions.Blocks.Common;

/// <summary>
/// A contact block with forms, contact info, and map options.
/// </summary>
[BlockMetadata("aero_contact", "Aero Contact", Category = "Aero")]
public class AeroContactBlock : BlockBase
{
        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => "aero_contact";

        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string? Title { get; set; }
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description { get; set; }
        /// <summary>
    /// Gets or sets the Details.
    /// </summary>
public List<AeroContactDetail>? Details { get; set; } = new();
        /// <summary>
    /// Gets or sets the Form Action Url.
    /// </summary>
public string? FormActionUrl { get; set; }
        /// <summary>
    /// Gets or sets the Image Url.
    /// </summary>
public string? ImageUrl { get; set; }
        /// <summary>
    /// Gets or sets the Layout.
    /// </summary>
public AeroContactLayout Layout { get; set; } = AeroContactLayout.Card;

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}

/// <summary>
/// Represents a class for AeroContactDetail.
/// </summary>
public class AeroContactDetail
{
        /// <summary>
    /// Gets or sets the Icon.
    /// </summary>
public string? Icon { get; set; } // svg path or simple identifier
        /// <summary>
    /// Gets or sets the Label.
    /// </summary>
public string? Label { get; set; }
        /// <summary>
    /// Gets or sets the Value.
    /// </summary>
public string? Value { get; set; }
}

/// <summary>
/// Defines an enumeration for AeroContactLayout.
/// </summary>
public enum AeroContactLayout
{
    Simple,
    Card,
    Grid,
    TwoColumn,
    Image,
    Map
}
