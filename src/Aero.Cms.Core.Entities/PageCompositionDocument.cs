using Aero.Cms.Abstractions.Blocks.Layout;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Models;
using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Core.Entities;

/// <summary>
/// First-class persisted composition tree for a page draft or published version.
/// PageDocument remains the route/status aggregate; this document owns the nested
/// authoring tree used by the visual editor and public renderer.
/// </summary>
public sealed class PageCompositionDocument : SableDocument, IAuditable, ISiteOwned, IAuditableEntity
{
        /// <summary>
    /// Gets or sets the Site Id.
    /// </summary>
public long SiteId { get; set; }
        /// <summary>
    /// Gets or sets the Page Id.
    /// </summary>
public long PageId { get; set; }
        /// <summary>
    /// Gets or sets the Culture.
    /// </summary>
public string Culture { get; set; } = SitesModel.DefaultCultureName;
        /// <summary>
    /// Gets or sets the State.
    /// </summary>
public PageCompositionState State { get; set; } = PageCompositionState.Draft;
        /// <summary>
    /// Gets or sets the Content Revision.
    /// </summary>
public long ContentRevision { get; set; }
        /// <summary>
    /// Gets or sets the Published Version.
    /// </summary>
public long PublishedVersion { get; set; }
        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string Title { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Slug.
    /// </summary>
public string Slug { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Summary.
    /// </summary>
public string? Summary { get; set; }
        /// <summary>
    /// Gets or sets the Seo Title.
    /// </summary>
public string? SeoTitle { get; set; }
        /// <summary>
    /// Gets or sets the Seo Description.
    /// </summary>
public string? SeoDescription { get; set; }
        /// <summary>
    /// Gets or sets the Kind.
    /// </summary>
public PageKind Kind { get; set; } = PageKind.Standard;
        /// <summary>
    /// Gets or sets the Show Header Navigation.
    /// </summary>
public bool ShowHeaderNavigation { get; set; } = true;
        /// <summary>
    /// Gets or sets the Header Image Url.
    /// </summary>
public string? HeaderImageUrl { get; set; }
        /// <summary>
    /// Gets or sets the Hide Header.
    /// </summary>
public bool HideHeader { get; set; }
        /// <summary>
    /// Gets or sets the Hide Footer.
    /// </summary>
public bool HideFooter { get; set; }
        /// <summary>
    /// Gets or sets the Show Chat Agent.
    /// </summary>
public bool ShowChatAgent { get; set; } = true;
        /// <summary>
    /// Gets or sets the Root Nodes.
    /// </summary>
public List<NeoPageNode> RootNodes { get; set; } = [];

    /// <summary>
    /// Temporary compatibility bridge for legacy render surfaces. New code should
    /// render <see cref="RootNodes"/> directly.
    /// </summary>
    public List<LayoutRegion> LayoutRegions { get; set; } = [];

        /// <summary>
    /// Gets or sets the Block Id Map.
    /// </summary>
    public Dictionary<string, long> BlockIdMap { get; set; } = [];

    // IAuditable
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ModifiedOn { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
}

/// <summary>
/// Defines an enumeration for PageCompositionState.
/// </summary>
public enum PageCompositionState
{
    Draft = 0,
    Published = 1,
    Archived = 2
}
