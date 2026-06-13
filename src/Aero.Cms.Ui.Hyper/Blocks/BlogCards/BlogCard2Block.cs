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
    public const string BlockTypeId = "hyper.blog-cards.2";

    public override string BlockType => BlockTypeId;

    public string ImageUrl { get; set; } = "https://images.unsplash.com/photo-1631451095765-2c91616fc9e6?auto=format&fit=crop&q=80&w=1160";
    public string MainText { get; set; } = "Finding the Journey to Mordor";
    public string Description { get; set; } = "Lorem ipsum dolor sit amet, consectetur adipisicing elit. Recusandae dolores, possimus pariatur animi temporibus nesciunt praesentium dolore sed nulla ipsum eveniet corporis quidem, mollitia itaque minus soluta, voluptates neque explicabo tempora nisi culpa eius atque dignissimos. Molestias explicabo corporis voluptatem?";
    public string CtaUrl { get; set; } = "#";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
