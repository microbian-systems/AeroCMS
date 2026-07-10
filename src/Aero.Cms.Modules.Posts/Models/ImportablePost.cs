namespace Aero.Cms.Modules.Posts.Models;

/// <summary>
/// Represents a blog post parsed from an import file (JSON, MD, or ZIP).
/// </summary>
public sealed record ImportablePost
{
        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string Title { get; init; } = string.Empty;
        /// <summary>
    /// Gets or sets the Slug.
    /// </summary>
public string Slug { get; init; } = string.Empty;
        /// <summary>
    /// Gets or sets the Markdown Content.
    /// </summary>
public string MarkdownContent { get; init; } = string.Empty;
        /// <summary>
    /// Gets or sets the Cover Image.
    /// </summary>
public string? CoverImage { get; init; }
        /// <summary>
    /// Gets or sets the Published On.
    /// </summary>
public DateTimeOffset? PublishedOn { get; init; }
        /// <summary>
    /// Gets or sets the Tags.
    /// </summary>
public List<string> Tags { get; init; } = [];
}
