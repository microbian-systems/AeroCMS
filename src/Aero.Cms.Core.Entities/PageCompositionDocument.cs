using Aero.Cms.Abstractions.Blocks.Layout;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Models;
using Aero.Core.Entities;

namespace Aero.Cms.Core.Entities;

/// <summary>
/// First-class persisted composition tree for a page draft or published version.
/// PageDocument remains the route/status aggregate; this document owns the nested
/// authoring tree used by the visual editor and public renderer.
/// </summary>
public sealed class PageCompositionDocument : Entity, ISiteOwned, IAuditableEntity
{
    public long SiteId { get; set; }
    public long PageId { get; set; }
    public string Culture { get; set; } = SitesModel.DefaultCultureName;
    public PageCompositionState State { get; set; } = PageCompositionState.Draft;
    public long ContentRevision { get; set; }
    public long PublishedVersion { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
    public PageKind Kind { get; set; } = PageKind.Standard;
    public bool ShowHeaderNavigation { get; set; } = true;
    public string? HeaderImageUrl { get; set; }
    public bool HideHeader { get; set; }
    public bool HideFooter { get; set; }
    public bool ShowChatAgent { get; set; } = true;
    public List<NeoPageNode> RootNodes { get; set; } = [];

    /// <summary>
    /// Temporary compatibility bridge for legacy render surfaces. New code should
    /// render <see cref="RootNodes"/> directly.
    /// </summary>
    public List<LayoutRegion> LayoutRegions { get; set; } = [];

    public Dictionary<string, long> BlockIdMap { get; set; } = [];
}

public enum PageCompositionState
{
    Draft = 0,
    Published = 1,
    Archived = 2
}
