namespace Aero.Cms.Modules.Posts.Models;

/// <summary>
/// Represents the parser-neutral data extracted for one candidate blog post.
/// </summary>
public sealed record ImportablePost
{
    /// <summary>
    /// Gets the imported title, or an empty string when the source omitted it.
    /// </summary>
public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Gets the imported or parser-generated slug.
    /// </summary>
public string Slug { get; init; } = string.Empty;

    /// <summary>
    /// Gets the post body in Markdown form.
    /// </summary>
public string MarkdownContent { get; init; } = string.Empty;

    /// <summary>
    /// Gets an optional cover-image URL supplied by the import.
    /// </summary>
public string? CoverImage { get; init; }

    /// <summary>
    /// Gets the parsed source publication time, or <see langword="null"/> when unavailable or invalid.
    /// </summary>
public DateTimeOffset? PublishedOn { get; init; }

    /// <summary>
    /// Gets the tag names supplied by the import.
    /// </summary>
public List<string> Tags { get; init; } = [];
}
