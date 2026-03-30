using Aero.Cms.Core.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Core.Blocks.Common;

/// <summary>
/// A testimonials block for social proof.
/// </summary>
[BlockMetadata("aero_testimonials", "Aero Testimonials", Category = "Aero")]
public class AeroTestimonialsBlock : BlockBase
{
    public override string BlockType => "aero_testimonials";

    public string? Title { get; set; }
    public string? Description { get; set; }
    public List<AeroTestimonialItem> Testimonials { get; set; } = new();
    public string? AeroLayout { get; set; } = "Grid"; // Grid, Slider, Simple

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}

public class AeroTestimonialItem
{
    public string? AuthorName { get; set; }
    public string? AuthorRole { get; set; }
    public string? AuthorImage { get; set; }
    public string? Content { get; set; }
    public int StarRating { get; set; } = 5;
    public string? CompanyName { get; set; }
}
