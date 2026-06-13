using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Announcements;

/// <summary>
/// HyperUI Announcements 4 — fixed bottom banner bar with dismiss button.
/// Source: hyperui/public/examples/marketing/announcements/4.html.
/// </summary>
[BlockMetadata(
    "hyper.announcements.4",
    "Announcement 4",
    Category = "Hyper",
    Icon = "info",
    SortOrder = 84,
    SchemaVersion = 1)]
public sealed class Announcement4Block : BlockBase
{
    public const string BlockTypeId = "hyper.announcements.4";

    public override string BlockType => BlockTypeId;

    public string MainText { get; set; } = "Lorem, ipsum dolor";
    public string CtaText { get; set; } = "sit amet consectetur";
    public string CtaUrl { get; set; } = "#";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
