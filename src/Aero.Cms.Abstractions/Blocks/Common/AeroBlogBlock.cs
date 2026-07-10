using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Abstractions.Blocks.Common;

/// <summary>
/// A blog post grid block for displaying latest stories.
/// </summary>
[BlockMetadata("aero_blog", "Aero Blog", Category = "Aero")]
public class AeroBlogBlock : BlockBase
{
        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => "aero_blog";

        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string? Title { get; set; }
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description { get; set; }
        /// <summary>
    /// Gets or sets the Posts.
    /// </summary>
public List<AeroBlogItem> Posts { get; set; } = new();
        /// <summary>
    /// Gets or sets the Aero Layout.
    /// </summary>
public string? AeroLayout { get; set; } = "Cards"; // Cards, List, Large

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}

/// <summary>
/// Represents a class for AeroBlogItem.
/// </summary>
public class AeroBlogItem
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
    /// Gets or sets the Image Url.
    /// </summary>
public string? ImageUrl { get; set; }
        /// <summary>
    /// Gets or sets the Author Name.
    /// </summary>
public string? AuthorName { get; set; }
        /// <summary>
    /// Gets or sets the Published At.
    /// </summary>
public string? PublishedAt { get; set; }
        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public string? Category { get; set; }
        /// <summary>
    /// Gets or sets the Post Url.
    /// </summary>
public string? PostUrl { get; set; }
}
