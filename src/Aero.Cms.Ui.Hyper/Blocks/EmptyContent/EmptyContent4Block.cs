using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.EmptyContent;

/// <summary>
/// HyperUI Empty Content 4 — "Explore more" with link cards and back to shopping CTA.
/// Source: hyperui/public/examples/marketing/empty-content/4.html + 4-dark.html.
/// </summary>
[BlockMetadata(
    "hyper.empty-content.4",
    "Empty Content 4",
    Category = "Hyper",
    Icon = "inbox",
    SortOrder = 121,
    SchemaVersion = 1)]
public sealed class EmptyContent4Block : BlockBase
{
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.empty-content.4";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string Title { get; set; } = "Explore more";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string Description { get; set; } = "This section doesn't have content right now. Discover related topics and inspiration instead.";
        /// <summary>
    /// Gets or sets the Cta Text.
    /// </summary>
public string CtaText { get; set; } = "Back to Shopping";
        /// <summary>
    /// Gets or sets the Cta Url.
    /// </summary>
public string CtaUrl { get; set; } = "#";
        /// <summary>
    /// Gets or sets the Links.
    /// </summary>
public List<EmptyContentLink> Links { get; set; } = DefaultLinks.Select(CloneLink).ToList();

        /// <summary>
    /// DefaultLinks.
    /// </summary>
public static readonly List<EmptyContentLink> DefaultLinks =
    [
        new() { Title = "Featured Collection", Description = "Browse our curated selection", Url = "#" },
        new() { Title = "Latest Trends", Description = "See what's trending now", Url = "#" }
    ];

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);

    private static EmptyContentLink CloneLink(EmptyContentLink l) => new()
    {
        Title = l.Title,
        Description = l.Description,
        Url = l.Url
    };
}

/// <summary>
/// Represents a class for EmptyContentLink.
/// </summary>
public sealed class EmptyContentLink
{
        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string Title { get; set; } = "";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string Description { get; set; } = "";
        /// <summary>
    /// Gets or sets the Url.
    /// </summary>
public string Url { get; set; } = "#";
}
