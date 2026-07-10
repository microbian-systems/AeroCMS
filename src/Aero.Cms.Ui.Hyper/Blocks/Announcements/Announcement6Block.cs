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
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.announcements.6";

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
