using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.BlogCards;

/// <summary>
/// HyperUI Blog Card 3 — bordered card with image, title, description, and "Find out more" link.
/// Source: hyperui/public/examples/marketing/blog-cards/3.html.
/// </summary>
[BlockMetadata(
    "hyper.blog-cards.3",
    "Blog Card 3",
    Category = "Hyper",
    Icon = "file-text",
    SortOrder = 89,
    SchemaVersion = 1)]
public sealed class BlogCard3Block : BlockBase
{
    public const string BlockTypeId = "hyper.blog-cards.3";

    public override string BlockType => BlockTypeId;

    public string ImageUrl { get; set; } = "https://images.unsplash.com/photo-1600880292203-757bb62b4baf?auto=format&fit=crop&q=80&w=1160";
    public string MainText { get; set; } = "Lorem ipsum dolor sit amet consectetur adipisicing elit.";
    public string Description { get; set; } = "Lorem ipsum dolor sit amet, consectetur adipisicing elit. Recusandae dolores, possimus pariatur animi temporibus nesciunt praesentium dolore sed nulla ipsum eveniet corporis quidem, mollitia itaque minus soluta, voluptates neque explicabo tempora nisi culpa eius atque dignissimos. Molestias explicabo corporis voluptatem?";
    public string CtaText { get; set; } = "Find out more";
    public string CtaUrl { get; set; } = "#";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
