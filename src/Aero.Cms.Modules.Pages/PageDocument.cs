using Aero.Core;
using Aero.Core.Entities;

namespace Aero.Cms.Modules.Pages;

public enum PageKind
{
    Standard = 0,
    Homepage = 1,
    BlogListing = 2,
    Custom = 3
}

public sealed class PageDocument : Entity
{
    public PageKind Kind { get; set; } = PageKind.Standard;
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
    public string Body { get; set; } = string.Empty;
    public ContentPublicationState PublicationState { get; set; } = ContentPublicationState.Draft;
    public DateTimeOffset? PublishedOn { get; set; } = null;
    public bool IsPubliclyVisible => PublicationState == ContentPublicationState.Published;
}

public static class PageDocumentIds
{
    public static readonly long Homepage = Snowflake.NewId();
    public static readonly long BlogListing = Snowflake.NewId();
}
