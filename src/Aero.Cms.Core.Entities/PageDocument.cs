using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Layout;
using Aero.Cms.Core;
using Aero.Core.Entities;

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

    /// <summary>
    ///  Flag to hide the header on the page
    /// </summary>
    public bool HideHeader { get; set; } = false;
    /// <summary>
    ///  Flag to hide the footer on the page
    /// </summary>
    public bool HideFooter { get; set; } = false;

    /// <summary>
    /// Gets or sets whether the chat agent widget should be shown on this page.
    /// </summary>
    public bool ShowChatAgent { get; set; } = true;
}
