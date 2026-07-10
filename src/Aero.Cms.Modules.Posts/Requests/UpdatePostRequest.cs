using Aero.Cms.Abstractions.Enums;

namespace Aero.Cms.Modules.Posts.Requests;

/// <summary>
/// Represents a record for UpdatePostRequest.
/// </summary>
public sealed record UpdatePostRequest
{
        /// <summary>
    /// Gets or sets the Id.
    /// </summary>
public required long Id { get; init; }
        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public required string Title { get; init; }
        /// <summary>
    /// Gets or sets the Slug.
    /// </summary>
public required string Slug { get; init; }
        /// <summary>
    /// Gets or sets the Summary.
    /// </summary>
public string? Summary { get; init; }
        /// <summary>
    /// Gets or sets the Seo Title.
    /// </summary>
public string? SeoTitle { get; init; }
        /// <summary>
    /// Gets or sets the Seo Description.
    /// </summary>
public string? SeoDescription { get; init; }
        /// <summary>
    /// Gets or sets the Markdown Content.
    /// </summary>
public string? MarkdownContent { get; init; }
        /// <summary>
    /// Gets or sets the Tags.
    /// </summary>
public List<string>? Tags { get; init; }
        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public string? Category { get; init; }
        /// <summary>
    /// Gets or sets the Series Id.
    /// </summary>
public long? SeriesId { get; init; }
        /// <summary>
    /// Gets or sets the Author.
    /// </summary>
public string? Author { get; init; }
        /// <summary>
    /// Gets or sets the Image Url.
    /// </summary>
public string? ImageUrl { get; init; }
        /// <summary>
    /// Gets or sets the Publication State.
    /// </summary>
public ContentPublicationState PublicationState { get; init; } = ContentPublicationState.Draft;
}
