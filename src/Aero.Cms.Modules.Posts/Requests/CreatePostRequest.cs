using Aero.Cms.Abstractions.Enums;

namespace Aero.Cms.Modules.Posts.Requests;

/// <summary>
/// Describes the editable fields accepted when creating a blog post.
/// </summary>
public sealed record CreatePostRequest
{
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
    /// Gets the optional Markdown body; the endpoint stores an empty body when omitted.
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
    /// Gets the optional series identifier; the actor assigns the General series when absent.
    /// </summary>
public long? SeriesId { get; init; }

    /// <summary>
    /// Gets an optional author name supplied by the client.
    /// </summary>
public string? Author { get; init; }

    /// <summary>
    /// Gets the optional featured-image URL.
    /// </summary>
public string? ImageUrl { get; init; }

    /// <summary>
    /// Gets the initial publication state.
    /// </summary>
public ContentPublicationState PublicationState { get; init; } = ContentPublicationState.Draft;
}
