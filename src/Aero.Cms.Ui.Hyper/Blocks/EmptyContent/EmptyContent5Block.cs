using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.EmptyContent;

/// <summary>
/// HyperUI Empty Content 5 — "Out of stock" with notify and explore buttons.
/// Source: hyperui/public/examples/marketing/empty-content/5.html + 5-dark.html.
/// </summary>
[BlockMetadata(
    "hyper.empty-content.5",
    "Empty Content 5",
    Category = "Hyper",
    Icon = "inbox",
    SortOrder = 122,
    SchemaVersion = 1)]
public sealed class EmptyContent5Block : BlockBase
{
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.empty-content.5";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string Title { get; set; } = "Out of stock";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string Description { get; set; } = "This item is currently unavailable. Check back soon or explore similar products.";
        /// <summary>
    /// Gets or sets the Cta Text.
    /// </summary>
public string CtaText { get; set; } = "Notify When Available";
        /// <summary>
    /// Gets or sets the Cta Url.
    /// </summary>
public string CtaUrl { get; set; } = "#";
        /// <summary>
    /// Gets or sets the Cta Text2.
    /// </summary>
public string CtaText2 { get; set; } = "Explore Similar Products";
        /// <summary>
    /// Gets or sets the Cta Url2.
    /// </summary>
public string CtaUrl2 { get; set; } = "#";
        /// <summary>
    /// Gets or sets the Status Text.
    /// </summary>
public string StatusText { get; set; } = "Last restocked: 3 weeks ago";

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
