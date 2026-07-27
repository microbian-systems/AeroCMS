using Aero.Cms.Abstractions.Enums;

namespace Aero.Cms.Modules.Posts.Requests;

/// <summary>
/// Describes the editable fields accepted when updating a blog post.
/// </summary>
public sealed record UpdatePostRequest
{
    /// <summary>
    /// Gets the positive post identifier.
    /// </summary>
public required long Id { get; init; }

    /// <summary>
    /// Gets the required display title.
    /// </summary>
public required string Title { get; init; }

    /// <summary>
    /// Gets the required lowercase, hyphen-delimited route slug.
    /// </summary>
public required string Slug { get; init; }

    /// <summary>
    /// Gets the optional post summary.
    /// </summary>
public string? Summary { get; init; }

    /// <summary>
    /// Gets the optional search-engine title override.
    /// </summary>
public string? SeoTitle { get; init; }

    /// <summary>
    /// Gets the optional search-engine description.
    /// </summary>
public string? SeoDescription { get; init; }

    /// <summary>
    /// Gets a replacement Markdown body; <see langword="null"/> leaves the stored body unchanged.
    /// </summary>
public string? MarkdownContent { get; init; }

    /// <summary>
    /// Gets optional tag names supplied by the client.
    /// </summary>
public List<string>? Tags { get; init; }

    /// <summary>
    /// Gets an optional category name supplied by the client.
    /// </summary>
public string? Category { get; init; }

    /// <summary>
    /// Gets the replacement series identifier.
    /// </summary>
public long? SeriesId { get; init; }

    /// <summary>
    /// Gets an optional author name supplied by the client.
    /// </summary>
public string? Author { get; init; }

    /// <summary>
    /// Gets the replacement featured-image URL.
    /// </summary>
public string? ImageUrl { get; init; }

    /// <summary>
    /// Gets the replacement publication state.
    /// </summary>
public ContentPublicationState PublicationState { get; init; } = ContentPublicationState.Draft;

    /// <summary>Gets whether the published post is eligible for site search.</summary>
    public bool IncludeInSearch { get; init; } = true;

    /// <summary>Gets whether the published post may ground public AI answers.</summary>
    public bool IncludeInPublicAi { get; init; }
}
