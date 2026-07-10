using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.EmptyContent;

/// <summary>
/// HyperUI Empty Content 1 — "Hmm, nothing found" with buttons and popular searches.
/// Source: hyperui/public/examples/marketing/empty-content/1.html + 1-dark.html.
/// </summary>
[BlockMetadata(
    "hyper.empty-content.1",
    "Empty Content 1",
    Category = "Hyper",
    Icon = "inbox",
    SortOrder = 118,
    SchemaVersion = 1)]
public sealed class EmptyContent1Block : BlockBase
{
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.empty-content.1";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string Title { get; set; } = "Hmm, nothing found";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string Description { get; set; } = "We couldn't find what you were looking for. Try a different search term or explore our popular categories.";
        /// <summary>
    /// Gets or sets the Cta Text.
    /// </summary>
public string CtaText { get; set; } = "Browse Popular Items";
        /// <summary>
    /// Gets or sets the Cta Url.
    /// </summary>
public string CtaUrl { get; set; } = "#";
        /// <summary>
    /// Gets or sets the Cta Text2.
    /// </summary>
public string CtaText2 { get; set; } = "Refine Search";
        /// <summary>
    /// Gets or sets the Cta Url2.
    /// </summary>
public string CtaUrl2 { get; set; } = "#";

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
