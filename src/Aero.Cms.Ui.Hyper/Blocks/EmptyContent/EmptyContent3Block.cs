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
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.empty-content.3";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string Title { get; set; } = "Coming soon!";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string Description { get; set; } = "We're working on something exciting. Be the first to know when it launches.";
        /// <summary>
    /// Gets or sets the Email Placeholder.
    /// </summary>
public string EmailPlaceholder { get; set; } = "your@email.com";
        /// <summary>
    /// Gets or sets the Submit Text.
    /// </summary>
public string SubmitText { get; set; } = "Notify Me";
        /// <summary>
    /// Gets or sets the Footnote.
    /// </summary>
public string Footnote { get; set; } = "We'll let you know the moment it's available.";

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
