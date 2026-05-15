using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.BlogCards;

/// <summary>
/// HyperUI Blog Card 5 — card with icon, title, description, and "Find out more" link.
/// Source: hyperui/public/examples/marketing/blog-cards/5.html.
/// </summary>
[BlockMetadata(
    "hyper.blog-cards.5",
    "Blog Card 5",
    Category = "Hyper",
    Icon = "file-text",
    SortOrder = 91,
    SchemaVersion = 1)]
public sealed class BlogCard5Block : BlockBase
{
    public const string BlockTypeId = "hyper.blog-cards.5";

    public override string BlockType => BlockTypeId;

    public string ImageUrl { get; set; } = "";
    public string MainText { get; set; } = "Lorem ipsum dolor sit, amet consectetur adipisicing elit.";
    public string Description { get; set; } = "Lorem ipsum dolor sit amet, consectetur adipisicing elit. Recusandae dolores, possimus pariatur animi temporibus nesciunt praesentium dolore sed nulla ipsum eveniet corporis quidem, mollitia itaque minus soluta, voluptates neque explicabo tempora nisi culpa eius atque dignissimos. Molestias explicabo corporis voluptatem?";
    public string CtaText { get; set; } = "Find out more";
    public string CtaUrl { get; set; } = "#";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
