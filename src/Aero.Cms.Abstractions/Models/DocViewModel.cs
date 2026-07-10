using Aero.Cms.Abstractions.Enums;

namespace Aero.Cms.Abstractions.Models;

/// <summary>
/// Represents a record for DocViewModel.
/// </summary>
[Alias("DocViewModel")]
[GenerateSerializer]
public sealed record DocViewModel : AeroEntityViewModel
{
        /// <summary>
    /// Gets or sets the Slug.
    /// </summary>
[Id(0)]
    public string? Slug { get; set; } 
        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
[Id(1)]
    public string? Title { get; set; } 
        /// <summary>
    /// Gets or sets the Summary.
    /// </summary>
[Id(2)]
    public string? Summary { get; set; }
        /// <summary>
    /// Gets or sets the Markdown Content.
    /// </summary>
[Id(3)]
    public string? MarkdownContent { get; set; }
        /// <summary>
    /// Gets or sets the Seo Title.
    /// </summary>
[Id(4)]
    public string? SeoTitle { get; set; }
        /// <summary>
    /// Gets or sets the Seo Description.
    /// </summary>
[Id(5)]
    public string? SeoDescription { get; set; }

        /// <summary>
    /// Gets or sets the Publication State.
    /// </summary>
[Id(6)]
    public ContentPublicationState PublicationState { get; set; } = ContentPublicationState.Draft;
        /// <summary>
    /// Gets or sets the Published On.
    /// </summary>
[Id(7)]
    public DateTimeOffset? PublishedOn { get; set; } = null;
        /// <summary>
    /// Gets or sets the Is Publicly Visible.
    /// </summary>
public bool IsPubliclyVisible => PublicationState == ContentPublicationState.Published;

    /// <summary>
    /// Gets or sets whether the global header navigation should be shown when viewing this page.
    /// </summary>
    [Id(8)]
    public bool ShowHeaderNavigation { get; set; } = true;

    /// <summary>
    /// Gets or sets an optional image URL to be used as a background for the page header/hero section.
    /// </summary>
    [Id(9)]
    public string? HeaderImageUrl { get; set; }

    /// <summary>
    /// Gets or sets the parent document ID for hierarchical structure.
    /// </summary>
    [Id(10)]
    public long? ParentId { get; set; }

    /// <summary>
    /// Gets or sets the sort order among siblings.
    /// </summary>
    [Id(11)]
    public int Order { get; set; }

    /// <summary>
    /// Gets or sets the last published layout/content version.
    /// </summary>
    [Id(12)]
    public long PublishedVersion { get; set; }

    /// <summary>
    /// Gets or sets the current block schema version for docs content.
    /// </summary>
    [Id(13)]
    public int BlockSchemaVersion { get; set; }

        /// <summary>
    /// Gets or sets the Culture.
    /// </summary>
[Id(14)]
    public string Culture { get; set; } = "en-US";

        /// <summary>
    /// Gets or sets the Translation Group Id.
    /// </summary>
[Id(15)]
    public long? TranslationGroupId { get; set; }
}

/// <summary>
/// Represents a record for DocErrorViewModel.
/// </summary>
[GenerateSerializer]
[Alias("DocErrorViewModel")]
public record DocErrorViewModel : AeroErrorViewModel<DocViewModel>;
