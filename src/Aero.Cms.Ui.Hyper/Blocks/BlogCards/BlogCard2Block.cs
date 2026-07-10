using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.BlogCards;

/// <summary>
/// HyperUI Blog Card 2 — image with shadow and title/description below, group hover effect.
/// Source: hyperui/public/examples/marketing/blog-cards/2.html.
/// </summary>
[BlockMetadata(
    "hyper.blog-cards.2",
    "Blog Card 2",
    Category = "Hyper",
    Icon = "file-text",
    SortOrder = 88,
    SchemaVersion = 1)]
public sealed class BlogCard2Block : BlockBase
{
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.blog-cards.2";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Image Url.
    /// </summary>
public string ImageUrl { get; set; } = "https://images.unsplash.com/photo-1631451095765-2c91616fc9e6?auto=format&fit=crop&q=80&w=1160";
        /// <summary>
    /// Gets or sets the Main Text.
    /// </summary>
public string MainText { get; set; } = "Finding the Journey to Mordor";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string Description { get; set; } = "Lorem ipsum dolor sit amet, consectetur adipisicing elit. Recusandae dolores, possimus pariatur animi temporibus nesciunt praesentium dolore sed nulla ipsum eveniet corporis quidem, mollitia itaque minus soluta, voluptates neque explicabo tempora nisi culpa eius atque dignissimos. Molestias explicabo corporis voluptatem?";
        /// <summary>
    /// Gets or sets the Cta Url.
    /// </summary>
public string CtaUrl { get; set; } = "#";

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
