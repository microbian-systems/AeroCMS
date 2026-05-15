using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.BlogCards;

/// <summary>
/// HyperUI Blog Card 4 — minimal card with date, title, and category tags.
/// Source: hyperui/public/examples/marketing/blog-cards/4.html.
/// </summary>
[BlockMetadata(
    "hyper.blog-cards.4",
    "Blog Card 4",
    Category = "Hyper",
    Icon = "file-text",
    SortOrder = 90,
    SchemaVersion = 1)]
public sealed class BlogCard4Block : BlockBase
{
    public const string BlockTypeId = "hyper.blog-cards.4";

    public override string BlockType => BlockTypeId;

    public string MainText { get; set; } = "How to center an element using JavaScript and jQuery";
    public string PublishedAt { get; set; } = "10th Oct 2022";
    public List<string> Tags { get; set; } = ["Snippet", "JavaScript"];
    public string CtaUrl { get; set; } = "#";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
