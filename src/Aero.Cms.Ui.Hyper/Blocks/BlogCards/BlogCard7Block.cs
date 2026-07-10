using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.BlogCards;

/// <summary>
/// HyperUI Blog Card 7 — overlay card with background image and gradient overlay.
/// Source: hyperui/public/examples/marketing/blog-cards/7.html.
/// </summary>
[BlockMetadata(
    "hyper.blog-cards.7",
    "Blog Card 7",
    Category = "Hyper",
    Icon = "file-text",
    SortOrder = 93,
    SchemaVersion = 1)]
public sealed class BlogCard7Block : BlockBase
{
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.blog-cards.7";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Image Url.
    /// </summary>
public string ImageUrl { get; set; } = "https://images.unsplash.com/photo-1661956602116-aa6865609028?auto=format&fit=crop&q=80&w=1160";
        /// <summary>
    /// Gets or sets the Main Text.
    /// </summary>
public string MainText { get; set; } = "How to position your furniture for positivity";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string Description { get; set; } = "Lorem ipsum dolor sit amet, consectetur adipisicing elit. Recusandae dolores, possimus pariatur animi temporibus nesciunt praesentium dolore sed nulla ipsum eveniet corporis quidem, mollitia itaque minus soluta, voluptates neque explicabo tempora nisi culpa eius atque dignissimos. Molestias explicabo corporis voluptatem?";
        /// <summary>
    /// Gets or sets the Published At.
    /// </summary>
public string PublishedAt { get; set; } = "10th Oct 2022";
        /// <summary>
    /// Gets or sets the Cta Url.
    /// </summary>
public string CtaUrl { get; set; } = "#";

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
