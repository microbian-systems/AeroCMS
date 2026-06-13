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
    public const string BlockTypeId = "hyper.blog-cards.1";

    public override string BlockType => BlockTypeId;

    public string ImageUrl { get; set; } = "https://images.unsplash.com/photo-1524758631624-e2822e304c36?auto=format&fit=crop&q=80&w=1160";
    public string MainText { get; set; } = "How to position your furniture for positivity";
    public string Description { get; set; } = "Lorem ipsum dolor sit amet, consectetur adipisicing elit. Recusandae dolores, possimus pariatur animi temporibus nesciunt praesentium dolore sed nulla ipsum eveniet corporis quidem, mollitia itaque minus soluta, voluptates neque explicabo tempora nisi culpa eius atque dignissimos. Molestias explicabo corporis voluptatem?";
    public string PublishedAt { get; set; } = "10th Oct 2022";
    public string CtaUrl { get; set; } = "#";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
