using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Announcements;

/// <summary>
/// HyperUI Announcements 6 — floating bottom banner with dismiss button.
/// Source: hyperui/public/examples/marketing/announcements/6.html.
/// </summary>
[BlockMetadata(
    "hyper.announcements.6",
    "Announcement 6",
    Category = "Hyper",
    Icon = "info",
    SortOrder = 86,
    SchemaVersion = 1)]
public sealed class Announcement6Block : BlockBase
{
    public const string BlockTypeId = "hyper.announcements.6";

    public override string BlockType => BlockTypeId;

    public string MainText { get; set; } = "Lorem, ipsum dolor";
    public string CtaText { get; set; } = "sit amet consectetur";
    public string CtaUrl { get; set; } = "#";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
