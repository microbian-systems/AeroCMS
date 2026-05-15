using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.EmptyContent;

/// <summary>
/// HyperUI Empty Content 1 — "Hmm, nothing found" with buttons and popular searches.
/// Source: hyperui/public/examples/marketing/empty-content/1.html + 1-dark.html.
/// </summary>
[BlockMetadata(
    "hyper.empty-content.1",
    "Empty Content 1",
    Category = "Hyper",
    Icon = "inbox",
    SortOrder = 118,
    SchemaVersion = 1)]
public sealed class EmptyContent1Block : BlockBase
{
    public const string BlockTypeId = "hyper.empty-content.1";

    public override string BlockType => BlockTypeId;

    public string Title { get; set; } = "Hmm, nothing found";
    public string Description { get; set; } = "We couldn't find what you were looking for. Try a different search term or explore our popular categories.";
    public string CtaText { get; set; } = "Browse Popular Items";
    public string CtaUrl { get; set; } = "#";
    public string CtaText2 { get; set; } = "Refine Search";
    public string CtaUrl2 { get; set; } = "#";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
