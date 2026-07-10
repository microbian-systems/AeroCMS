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
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.announcements.2";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Main Text.
    /// </summary>
public string MainText { get; set; } = "Lorem, ipsum dolor";
        /// <summary>
    /// Gets or sets the Cta Text.
    /// </summary>
public string CtaText { get; set; } = "sit amet consectetur";
        /// <summary>
    /// Gets or sets the Cta Url.
    /// </summary>
public string CtaUrl { get; set; } = "#";

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
