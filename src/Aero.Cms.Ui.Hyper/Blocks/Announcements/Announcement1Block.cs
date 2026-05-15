using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Announcements;

/// <summary>
/// HyperUI Announcements 1 — simple banner bar.
/// Source: hyperui/public/examples/marketing/announcements/1.html.
/// </summary>
[BlockMetadata(
    "hyper.announcements.1",
    "Announcement 1",
    Category = "Hyper",
    Icon = "info",
    SortOrder = 81,
    SchemaVersion = 1)]
public sealed class Announcement1Block : BlockBase
{
    public const string BlockTypeId = "hyper.announcements.1";

    public override string BlockType => BlockTypeId;

    public string MainText { get; set; } = "Lorem, ipsum dolor";
    public string CtaText { get; set; } = "sit amet consectetur";
    public string CtaUrl { get; set; } = "#";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
