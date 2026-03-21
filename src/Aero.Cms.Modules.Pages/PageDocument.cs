namespace Aero.Cms.Modules.Pages;

public enum PageKind
{
    Standard = 0,
    Homepage = 1,
    BlogListing = 2
}

public sealed class PageDocument
{
    public string Id { get; set; } = string.Empty;
    public PageKind Kind { get; set; } = PageKind.Standard;
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
    public string Body { get; set; } = string.Empty;
    public ContentPublicationState PublicationState { get; set; } = ContentPublicationState.Draft;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? PublishedAtUtc { get; set; }

    public bool IsPubliclyVisible => PublicationState == ContentPublicationState.Published;
}

public static class PageDocumentIds
{
    public const string Homepage = "cms/pages/home";
    public const string BlogListing = "cms/pages/blog";
}
