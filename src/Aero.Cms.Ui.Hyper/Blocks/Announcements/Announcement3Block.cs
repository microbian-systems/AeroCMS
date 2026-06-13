using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Announcements;

/// <summary>
/// HyperUI Announcements 3 — fixed bottom banner bar.
/// Source: hyperui/public/examples/marketing/announcements/3.html.
/// </summary>
[BlockMetadata(
    "hyper.announcements.3",
    "Announcement 3",
    Category = "Hyper",
    Icon = "info",
    SortOrder = 83,
    SchemaVersion = 1)]
public sealed class Announcement3Block : BlockBase
{
    public const string BlockTypeId = "hyper.announcements.3";

    public override string BlockType => BlockTypeId;

    public string MainText { get; set; } = "Lorem, ipsum dolor";
    public string CtaText { get; set; } = "sit amet consectetur";
    public string CtaUrl { get; set; } = "#";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
