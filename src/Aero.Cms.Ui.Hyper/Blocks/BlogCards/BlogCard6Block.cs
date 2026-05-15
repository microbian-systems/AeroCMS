using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.BlogCards;

/// <summary>
/// HyperUI Blog Card 6 — horizontal card with vertical date, image, content, and CTA button.
/// Source: hyperui/public/examples/marketing/blog-cards/6.html.
/// </summary>
[BlockMetadata(
    "hyper.blog-cards.6",
    "Blog Card 6",
    Category = "Hyper",
    Icon = "file-text",
    SortOrder = 92,
    SchemaVersion = 1)]
public sealed class BlogCard6Block : BlockBase
{
    public const string BlockTypeId = "hyper.blog-cards.6";

    public override string BlockType => BlockTypeId;

    public string ImageUrl { get; set; } = "https://images.unsplash.com/photo-1609557927087-f9cf8e88de18?auto=format&fit=crop&q=80&w=1160";
    public string MainText { get; set; } = "Finding the right guitar for your style - 5 tips";
    public string Description { get; set; } = "Lorem ipsum dolor sit amet, consectetur adipisicing elit. Recusandae dolores, possimus pariatur animi temporibus nesciunt praesentium dolore sed nulla ipsum eveniet corporis quidem, mollitia itaque minus soluta, voluptates neque explicabo tempora nisi culpa eius atque dignissimos. Molestias explicabo corporis voluptatem?";
    public string PublishedAt { get; set; } = "2022";
    public string PublishedAtDay { get; set; } = "Oct 10";
    public string CtaText { get; set; } = "Read Blog";
    public string CtaUrl { get; set; } = "#";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
