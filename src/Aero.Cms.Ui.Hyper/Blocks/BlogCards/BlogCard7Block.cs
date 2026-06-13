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
    public const string BlockTypeId = "hyper.blog-cards.7";

    public override string BlockType => BlockTypeId;

    public string ImageUrl { get; set; } = "https://images.unsplash.com/photo-1661956602116-aa6865609028?auto=format&fit=crop&q=80&w=1160";
    public string MainText { get; set; } = "How to position your furniture for positivity";
    public string Description { get; set; } = "Lorem ipsum dolor sit amet, consectetur adipisicing elit. Recusandae dolores, possimus pariatur animi temporibus nesciunt praesentium dolore sed nulla ipsum eveniet corporis quidem, mollitia itaque minus soluta, voluptates neque explicabo tempora nisi culpa eius atque dignissimos. Molestias explicabo corporis voluptatem?";
    public string PublishedAt { get; set; } = "10th Oct 2022";
    public string CtaUrl { get; set; } = "#";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
