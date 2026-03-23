using Aero.Core;
using Aero.Core.Entities;
using Aero.Cms.Modules.Pages.Models;

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
    
    /// <summary>
    /// Gets or sets the block-based layout regions for this page.
    /// </summary>
    public List<LayoutRegion> LayoutRegions { get; set; } = [];
    
    public ContentPublicationState PublicationState { get; set; } = ContentPublicationState.Draft;
    public DateTimeOffset? PublishedOn { get; set; } = null;
    public bool IsPubliclyVisible => PublicationState == ContentPublicationState.Published;
}
