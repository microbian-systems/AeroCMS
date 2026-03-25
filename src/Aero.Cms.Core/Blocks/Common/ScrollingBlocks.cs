using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Core.Blocks.Common;

/// <summary>
/// Defines the scrolling direction for content.
/// </summary>
public enum ScrollDirection
{
    HorizontalLeft = 0,
    HorizontalRight = 1,
    VerticalUp = 2,
    VerticalDown = 3
}

/// <summary>
/// An item within a scrolling content block.
/// </summary>
public sealed class ScrollingContentItem
{
    /// <summary>
    /// Gets or sets the media library ID for the item image (e.g., a customer logo or avatar).
    /// </summary>
    public long ImageMediaId { get; set; }

    /// <summary>
    /// Gets or sets a direct URL for the item image.
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Gets or sets the main text for the item (e.g., a quote).
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// Gets or sets the secondary text (e.g., name, company, or citation).
    /// </summary>
    public string? SubText { get; set; }

    /// <summary>
    /// Gets or sets an optional link URL for the item.
    /// </summary>
    public string? LinkUrl { get; set; }

    /// <summary>
    /// Gets or sets the alt text for the image.
    /// </summary>
    public string? AltText { get; set; }
}

/// <summary>
/// A block that renders scrolling content rows, such as logo clouds or infinite testimonial reels.
/// </summary>
[BlockMetadata("scrolling_content", "Scrolling Content", Category = "Layout")]
public sealed class ScrollingContentBlock : BlockBase
{
    public override string BlockType => "scrolling_content";

    /// <summary>
    /// Gets or sets the scrolling direction.
    /// </summary>
    public ScrollDirection Direction { get; set; } = ScrollDirection.HorizontalLeft;

    /// <summary>
    /// Gets or sets the scrolling speed (suggested range 1-100).
    /// </summary>
    public int Speed { get; set; } = 40;

    /// <summary>
    /// Gets or sets whether to pause the animation when the user hovers over the block.
    /// </summary>
    public bool PauseOnHover { get; set; } = true;

    /// <summary>
    /// Gets or sets the collection of items to scroll.
    /// </summary>
    public List<ScrollingContentItem> Items { get; set; } = new();

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
