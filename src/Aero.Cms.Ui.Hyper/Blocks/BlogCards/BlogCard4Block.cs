using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.BlogCards;

/// <summary>
/// HyperUI Blog Card 4 — minimal card with date, title, and category tags.
/// Source: hyperui/public/examples/marketing/blog-cards/4.html.
/// </summary>
[BlockMetadata(
    "hyper.blog-cards.4",
    "Blog Card 4",
    Category = "Hyper",
    Icon = "file-text",
    SortOrder = 90,
    SchemaVersion = 1)]
public sealed class BlogCard4Block : BlockBase
{
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.blog-cards.4";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Main Text.
    /// </summary>
public string MainText { get; set; } = "How to center an element using JavaScript and jQuery";
        /// <summary>
    /// Gets or sets the Published At.
    /// </summary>
public string PublishedAt { get; set; } = "10th Oct 2022";
        /// <summary>
    /// Gets or sets the Tags.
    /// </summary>
public List<string> Tags { get; set; } = ["Snippet", "JavaScript"];
        /// <summary>
    /// Gets or sets the Cta Url.
    /// </summary>
public string CtaUrl { get; set; } = "#";

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
