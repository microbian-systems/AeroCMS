using Aero.Cms.Abstractions.Enums;

namespace Aero.Cms.Abstractions.Models;

[Alias("DocViewModel")]
[GenerateSerializer]
public sealed record DocViewModel : AeroEntityViewModel
{
    [Id(0)]
    public string? Slug { get; set; } 
    [Id(1)]
    public string? Title { get; set; } 
    [Id(2)]
    public string? Summary { get; set; }
    [Id(3)]
    public string? MarkdownContent { get; set; }
    [Id(4)]
    public string? SeoTitle { get; set; }
    [Id(5)]
    public string? SeoDescription { get; set; }

    [Id(6)]
    public ContentPublicationState PublicationState { get; set; } = ContentPublicationState.Draft;
    [Id(7)]
    public DateTimeOffset? PublishedOn { get; set; } = null;
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
}

[GenerateSerializer]
[Alias("DocErrorViewModel")]
public record DocErrorViewModel : AeroErrorViewModel<DocViewModel>;
