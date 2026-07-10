using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Abstractions.Blocks.Common;

/// <summary>
/// A frequently asked questions (FAQ) block with collapsing or grid layouts.
/// </summary>
[BlockMetadata("aero_faq", "Aero FAQ", Category = "Aero")]
public class AeroFaqBlock : BlockBase
{
        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => "aero_faq";

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
public List<AeroFaqItem> Items { get; set; } = new();
        /// <summary>
    /// Gets or sets the Layout.
    /// </summary>
public AeroFaqLayout Layout { get; set; } = AeroFaqLayout.Collapse;

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}

/// <summary>
/// Represents a class for AeroFaqItem.
/// </summary>
public class AeroFaqItem
{
        /// <summary>
    /// Gets or sets the Question.
    /// </summary>
public string? Question { get; set; }
        /// <summary>
    /// Gets or sets the Answer.
    /// </summary>
public string? Answer { get; set; }
}

/// <summary>
/// Defines an enumeration for AeroFaqLayout.
/// </summary>
public enum AeroFaqLayout
{
    Collapse,
    Grid,
    Centered
}
