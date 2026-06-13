using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Announcements;

/// <summary>
/// HyperUI Announcements 2 — banner bar with dismiss button.
/// Source: hyperui/public/examples/marketing/announcements/2.html.
/// </summary>
[BlockMetadata(
    "hyper.announcements.2",
    "Announcement 2",
    Category = "Hyper",
    Icon = "info",
    SortOrder = 82,
    SchemaVersion = 1)]
public sealed class Announcement2Block : BlockBase
{
    public const string BlockTypeId = "hyper.announcements.2";

    public override string BlockType => BlockTypeId;

    public string MainText { get; set; } = "Lorem, ipsum dolor";
    public string CtaText { get; set; } = "sit amet consectetur";
    public string CtaUrl { get; set; } = "#";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
