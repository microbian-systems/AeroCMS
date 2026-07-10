using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Abstractions.Blocks.Common;

/// <summary>
/// A block for displaying features or services in various layouts (grid, cards, centered).
/// </summary>
[BlockMetadata("aero_features", "Aero Features", Category = "Aero")]
public class AeroFeaturesBlock : BlockBase
{
        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => "aero_features";

        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string? Title { get; set; }

        /// <summary>
    /// Gets or sets the Sub Title.
    /// </summary>
public string? SubTitle { get; set; }

        /// <summary>
    /// Gets or sets the Layout.
    /// </summary>
public AeroFeaturesLayout Layout { get; set; } = AeroFeaturesLayout.Simple;

        /// <summary>
    /// Gets or sets the Items.
    /// </summary>
public List<AeroFeatureItem> Items { get; set; } = new();

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}

/// <summary>
/// Represents a class for AeroFeatureItem.
/// </summary>
public class AeroFeatureItem
{
        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string? Title { get; set; }
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description { get; set; }
        /// <summary>
    /// Gets or sets the Icon.
    /// </summary>
public string? Icon { get; set; } // SVG path or name
        /// <summary>
    /// Gets or sets the Image Url.
    /// </summary>
public string? ImageUrl { get; set; }
        /// <summary>
    /// Gets or sets the Link Url.
    /// </summary>
public string? LinkUrl { get; set; }
}

/// <summary>
/// Defines an enumeration for AeroFeaturesLayout.
/// </summary>
public enum AeroFeaturesLayout
{
    Simple,
    Centered,
    Cards,
    GridList,
    Media
}
