using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.EmptyContent;

/// <summary>
/// HyperUI Empty Content 5 — "Out of stock" with notify and explore buttons.
/// Source: hyperui/public/examples/marketing/empty-content/5.html + 5-dark.html.
/// </summary>
[BlockMetadata(
    "hyper.empty-content.5",
    "Empty Content 5",
    Category = "Hyper",
    Icon = "inbox",
    SortOrder = 122,
    SchemaVersion = 1)]
public sealed class EmptyContent5Block : BlockBase
{
    public const string BlockTypeId = "hyper.empty-content.5";

    public override string BlockType => BlockTypeId;

    public string Title { get; set; } = "Out of stock";
    public string Description { get; set; } = "This item is currently unavailable. Check back soon or explore similar products.";
    public string CtaText { get; set; } = "Notify When Available";
    public string CtaUrl { get; set; } = "#";
    public string CtaText2 { get; set; } = "Explore Similar Products";
    public string CtaUrl2 { get; set; } = "#";
    public string StatusText { get; set; } = "Last restocked: 3 weeks ago";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
