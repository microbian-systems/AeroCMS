using Aero.Cms.Modules.Pages;

namespace Aero.Cms.Modules.Blog;

public sealed class BlogPostDocument
{
    public string Id { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Excerpt { get; set; }
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
    public string Body { get; set; } = string.Empty;
    public ContentPublicationState PublicationState { get; set; } = ContentPublicationState.Draft;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? PublishedAtUtc { get; set; }

    public bool IsPubliclyVisible => PublicationState == ContentPublicationState.Published;
}
