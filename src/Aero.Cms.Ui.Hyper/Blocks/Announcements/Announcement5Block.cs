using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Announcements;

/// <summary>
/// HyperUI Announcements 5 — floating bottom banner with rounded card.
/// Source: hyperui/public/examples/marketing/announcements/5.html.
/// </summary>
[BlockMetadata(
    "hyper.announcements.5",
    "Announcement 5",
    Category = "Hyper",
    Icon = "info",
    SortOrder = 85,
    SchemaVersion = 1)]
public sealed class Announcement5Block : BlockBase
{
    public const string BlockTypeId = "hyper.announcements.5";

    public override string BlockType => BlockTypeId;

    public string MainText { get; set; } = "Lorem, ipsum dolor";
    public string CtaText { get; set; } = "sit amet consectetur";
    public string CtaUrl { get; set; } = "#";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
