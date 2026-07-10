using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.BlogCards;

/// <summary>
/// HyperUI Blog Card 1 — classic card with image, date, title, and description.
/// Source: hyperui/public/examples/marketing/blog-cards/1.html.
/// </summary>
[BlockMetadata(
    "hyper.blog-cards.1",
    "Blog Card 1",
    Category = "Hyper",
    Icon = "file-text",
    SortOrder = 87,
    SchemaVersion = 1)]
public sealed class BlogCard1Block : BlockBase
{
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.blog-cards.1";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Image Url.
    /// </summary>
public string ImageUrl { get; set; } = "https://images.unsplash.com/photo-1524758631624-e2822e304c36?auto=format&fit=crop&q=80&w=1160";
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
