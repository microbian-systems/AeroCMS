using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.EmptyContent;

/// <summary>
/// HyperUI Empty Content 3 — "Coming soon!" with email notification form.
/// Source: hyperui/public/examples/marketing/empty-content/3.html + 3-dark.html.
/// </summary>
[BlockMetadata(
    "hyper.empty-content.3",
    "Empty Content 3",
    Category = "Hyper",
    Icon = "inbox",
    SortOrder = 120,
    SchemaVersion = 1)]
public sealed class EmptyContent3Block : BlockBase
{
    public const string BlockTypeId = "hyper.empty-content.3";

    public override string BlockType => BlockTypeId;

    public string Title { get; set; } = "Coming soon!";
    public string Description { get; set; } = "We're working on something exciting. Be the first to know when it launches.";
    public string EmailPlaceholder { get; set; } = "your@email.com";
    public string SubmitText { get; set; } = "Notify Me";
    public string Footnote { get; set; } = "We'll let you know the moment it's available.";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
