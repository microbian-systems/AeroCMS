using Aero.Core.Entities;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Core.Blocks.Common;

/// <summary>
/// Displays a rotating carousel of images.
/// </summary>
[BlockMetadata("carousel", "Carousel", Category = "Media")]
public sealed class CarouselBlock : BlockBase
{
    public override string BlockType => "carousel";

    /// <summary>
    /// The list of images and captions in the carousel.
    /// </summary>
    public List<CarouselItem> Items { get; set; } = new();

    /// <summary>
    /// Location of the carousel controls (e.g. bottom, overlay, hidden).
    /// </summary>
    public string ControlLocation { get; set; } = "bottom";

    /// <summary>
    /// Whether the carousel should automatically loop.
    /// </summary>
    public bool AutoPlay { get; set; } = true;

    /// <summary>
    /// Time between slides in milliseconds.
    /// </summary>
    public int Interval { get; set; } = 5000;

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}

/// <summary>
/// A single item in a <see cref="CarouselBlock"/>.
/// </summary>
public class CarouselItem : Entity
{
    /// <summary>
    /// Media asset ID for the image.
    /// </summary>
    public long ImageMediaId { get; set; }

    /// <summary>
    /// Optional display caption.
    /// </summary>
    public string? Caption { get; set; }

    /// <summary>
    /// Accessibility text for the image.
    /// </summary>
    public string? AltText { get; set; }

    /// <summary>
    /// Optional URL to visit if clicked.
    /// </summary>
    public string? ActionUrl { get; set; }
}

/// <summary>
/// A block that provides a link to internal CMS content (e.g., another page).
/// </summary>
[BlockMetadata("content_link", "Content Link", Category = "Navigation")]
public sealed class ContentLinkBlock : BlockBase
{
    public override string BlockType => "content_link";

    /// <summary>
    /// The target page ID to link to.
    /// </summary>
    public long TargetPageId { get; set; }

    /// <summary>
    /// The text to display for the link.
    /// </summary>
    public string LinkText { get; set; } = string.Empty;

    /// <summary>
    /// Optional icon class (e.g. font-awesome or heroicon).
    /// </summary>
    public string? IconClass { get; set; }

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}

/// <summary>
/// A prominent hero section with a background image, text overlay, and call-to-action.
/// </summary>
[BlockMetadata("hero", "Hero Section", Category = "Media")]
public sealed class HeroBlock : BlockBase
{
    public override string BlockType => "hero";

    /// <summary>
    /// Gets or sets the media asset ID for the background image.
    /// </summary>
    public long BackgroundImageMediaId { get; set; }

    /// <summary>
    /// Gets or sets an optional direct URL for the background image.
    /// </summary>
    public string? BackgroundImageUrl { get; set; }

    /// <summary>
    /// Gets or sets the accessibility alt text for the background image.
    /// </summary>
    public string? AltText { get; set; }

    /// <summary>
    /// Gets or sets whether to apply a parallax scrolling effect to the background.
    /// </summary>
    public bool UseParallax { get; set; }

    /// <summary>
    /// Gets or sets the main heading text.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the secondary sub-heading or description text.
    /// </summary>
    public string? SubTitle { get; set; }

    /// <summary>
    /// Gets or sets the label for the call-to-action button.
    /// </summary>
    public string? CtaText { get; set; }

    /// <summary>
    /// Gets or sets the target URL for the call-to-action button.
    /// </summary>
    public string? CtaUrl { get; set; }

    /// <summary>
    /// Gets or sets the opacity of the color overlay (0.0 to 1.0).
    /// </summary>
    public float OverlayOpacity { get; set; } = 0.4f;

    /// <summary>
    /// Gets or sets the text alignment for the hero content (e.g., center, left, right).
    /// </summary>
    public string TextAlignment { get; set; } = "center";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
