using Aero.Cms.Modules.Pages;
using Aero.Core.Entities;

namespace Aero.Cms.Modules.Blog;

public sealed class BlogPostDocument : Entity
{
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Excerpt { get; set; }
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset? PublishedOn { get; set; } = null;
    public ContentPublicationState PublicationState { get; set; } = ContentPublicationState.Draft;


    public bool IsPubliclyVisible => PublicationState == ContentPublicationState.Published;
}
