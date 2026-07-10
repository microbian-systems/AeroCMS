using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Abstractions.Blocks.Common;

/// <summary>
/// A portfolio showcase block for displaying project cards or visual galleries.
/// </summary>
[BlockMetadata("aero_portfolio", "Aero Portfolio", Category = "Aero")]
public class AeroPortfolioBlock : BlockBase
{
        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => "aero_portfolio";

        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string? Title { get; set; }
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description { get; set; }
        /// <summary>
    /// Gets or sets the Items.
    /// </summary>
public List<AeroPortfolioItem> Items { get; set; } = new();
        /// <summary>
    /// Gets or sets the Layout.
    /// </summary>
public AeroPortfolioLayout Layout { get; set; } = AeroPortfolioLayout.Cards;

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}

/// <summary>
/// Represents a class for AeroPortfolioItem.
/// </summary>
public class AeroPortfolioItem
{
        /// <summary>
    /// Gets or sets the Project Title.
    /// </summary>
public string? ProjectTitle { get; set; }
        /// <summary>
    /// Gets or sets the Project Category.
    /// </summary>
public string? ProjectCategory { get; set; }
        /// <summary>
    /// Gets or sets the Project Image Url.
    /// </summary>
public string? ProjectImageUrl { get; set; }
        /// <summary>
    /// Gets or sets the Project Url.
    /// </summary>
public string? ProjectUrl { get; set; }
        /// <summary>
    /// Gets or sets the Project Description.
    /// </summary>
public string? ProjectDescription { get; set; }
}

/// <summary>
/// Defines an enumeration for AeroPortfolioLayout.
/// </summary>
public enum AeroPortfolioLayout
{
    Cards,
    Centered,
    HoverEffect,
    Filter
}
