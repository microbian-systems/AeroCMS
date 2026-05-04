namespace Aero.Cms.Modules.Blog.Models;

/// <summary>
/// Represents a blog post parsed from an import file (JSON, MD, or ZIP).
/// </summary>
public sealed record ImportablePost
{
    public string Title { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string MarkdownContent { get; init; } = string.Empty;
    public string? CoverImage { get; init; }
    public DateTimeOffset? PublishedOn { get; init; }
    public List<string> Tags { get; init; } = [];
}
