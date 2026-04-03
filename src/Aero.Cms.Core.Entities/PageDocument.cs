using Aero.Cms.Core;
using Aero.Core.Entities;
using Aero.Cms.Core.Blocks;
using Aero.Cms.Core.Blocks.Layout;
using Aero.Cms.Abstractions.Enums;

namespace Aero.Cms.Core.Entities;

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

    /// <summary>
    /// Gets or sets the original editor blocks used to construct this page.
    /// Used natively by the page editor for state recovery.
    /// </summary>
    public List<EditorBlock> Blocks { get; set; } = [];

    public ContentPublicationState PublicationState { get; set; } = ContentPublicationState.Draft;
    public DateTimeOffset? PublishedOn { get; set; } = null;
    public bool IsPubliclyVisible => PublicationState == ContentPublicationState.Published;

    /// <summary>
    /// Gets or sets whether this page should be displayed in the main navigation menu.
    /// </summary>
    public bool ShowInNavMenu { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the global header navigation should be shown when viewing this page.
    /// </summary>
    public bool ShowHeaderNavigation { get; set; } = true;

    /// <summary>
    /// Gets or sets an optional image URL to be used as a background for the page header/hero section.
    /// </summary>
    public string? HeaderImageUrl { get; set; }
}
