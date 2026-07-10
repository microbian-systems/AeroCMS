using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Abstractions.Blocks.Common;

/// <summary>
/// A testimonials block for social proof.
/// </summary>
[BlockMetadata("aero_testimonials", "Aero Testimonials", Category = "Aero")]
public class AeroTestimonialsBlock : BlockBase
{
        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => "aero_testimonials";

        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string? Title { get; set; }
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description { get; set; }
        /// <summary>
    /// Gets or sets the Testimonials.
    /// </summary>
public List<AeroTestimonialItem> Testimonials { get; set; } = new();
        /// <summary>
    /// Gets or sets the Aero Layout.
    /// </summary>
public string? AeroLayout { get; set; } = "Grid"; // Grid, Slider, Simple

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}

/// <summary>
/// Represents a class for AeroTestimonialItem.
/// </summary>
public class AeroTestimonialItem
{
        /// <summary>
    /// Gets or sets the Author Name.
    /// </summary>
public string? AuthorName { get; set; }
        /// <summary>
    /// Gets or sets the Author Role.
    /// </summary>
public string? AuthorRole { get; set; }
        /// <summary>
    /// Gets or sets the Author Image.
    /// </summary>
public string? AuthorImage { get; set; }
        /// <summary>
    /// Gets or sets the Content.
    /// </summary>
public string? Content { get; set; }
        /// <summary>
    /// Gets or sets the Star Rating.
    /// </summary>
public int StarRating { get; set; } = 5;
        /// <summary>
    /// Gets or sets the Company Name.
    /// </summary>
public string? CompanyName { get; set; }
}
