using Aero.Cms.Abstractions.Enums;

namespace Aero.Cms.Abstractions.Models;

/// <summary>
/// Represents a record for PostViewModel.
/// </summary>
[Alias("PostViewModel")]
[GenerateSerializer]
public sealed record PostViewModel : AeroEntityViewModel
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
    /// Gets or sets the Excerpt.
    /// </summary>
[Id(2)]
    public string? Excerpt { get; set; }
        /// <summary>
    /// Gets or sets the Seo Title.
    /// </summary>
[Id(3)]
    public string? SeoTitle { get; set; }
        /// <summary>
    /// Gets or sets the Seo Description.
    /// </summary>
[Id(4)]
    public string? SeoDescription { get; set; }
        /// <summary>
    /// Gets or sets the Published On.
    /// </summary>
[Id(5)]
    public DateTimeOffset? PublishedOn { get; set; } = null;
        /// <summary>
    /// Gets or sets the Publication State.
    /// </summary>
[Id(6)]
    public ContentPublicationState PublicationState { get; set; } = ContentPublicationState.Draft;

    /// <summary>
    /// Gets or sets the Markdown body for this blog post.
    /// </summary>
    [Id(7)]
    public string MarkdownContent { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the IDs of tags associated with this blog post.
    /// </summary>
    [Id(8)]
    public List<long> TagIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the IDs of categories associated with this blog post.
    /// </summary>
    [Id(9)]
    public List<long> CategoryIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the ID of the author for this blog post.
    /// </summary>
    [Id(10)]
    public long? AuthorId { get; set; }

    /// <summary>
    /// Gets or sets the URL of the featured image for this blog post.
    /// </summary>
    [Id(11)]
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Gets or sets the number of likes for this blog post.
    /// </summary>
    [Id(12)]
    public int Likes { get; set; }
        /// <summary>
    /// Gets or sets the Culture.
    /// </summary>
[Id(13)]
    public string Culture { get; set; } = "en-US";
        /// <summary>
    /// Gets or sets the Translation Group Id.
    /// </summary>
[Id(14)]
    public long? TranslationGroupId { get; set; }
        /// <summary>
    /// Gets or sets the Series Id.
    /// </summary>
[Id(15)]
    public long? SeriesId { get; set; }

        /// <summary>
    /// Gets or sets the Is Publicly Visible.
    /// </summary>
public bool IsPubliclyVisible => PublicationState == ContentPublicationState.Published;
}


/// <summary>
/// Represents a record for PostErrorViewModel.
/// </summary>
[GenerateSerializer]
[Alias("PostErrorViewModel")]
public record PostErrorViewModel : AeroErrorViewModel<PostViewModel>;
