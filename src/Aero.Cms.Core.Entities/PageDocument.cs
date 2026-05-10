using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Layout;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Events;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Core;
using Aero.Core.Entities;
using Marten.Metadata;
using System.Diagnostics;

namespace Aero.Cms.Core.Entities;


public sealed class PageDocument : Entity, ISiteOwned, ISoftDeleted, IAuditableEntity
{
    public long SiteId { get; set; }
    public PageKind Kind { get; set; } = PageKind.Standard;
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }

    // ── Hierarchy ───────────────────────────────────────────────────────

    /// <summary>
    /// Parent page ID. <c>null</c> for root-level pages.
    /// </summary>
    public long? ParentId { get; set; }

    /// <summary>
    /// Full materialized path (e.g. "/sports/basketball/nba").
    /// </summary>
    public string Path { get; set; } = "/";

    /// <summary>
    /// Distance from root. 0 = root, 1 = direct child, etc.
    /// </summary>
    public int Depth { get; set; }

    /// <summary>
    /// Display order among siblings. Lower = first. Insertions require renumbering.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// When <c>true</c>, this page and all descendants are hidden from navigation menus.
    /// </summary>
    public bool IsHidden { get; set; }

    // ── Layout ───────────────────────────────────────────────────────────

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

    // ── Soft Delete ──────────────────────────────────────────────────────

    /// <summary>
    /// <c>true</c> when the page has been soft-deleted via Marten.
    /// Managed automatically by Marten's <c>ISoftDeleted</c> policy.
    /// </summary>
    public bool Deleted { get; set; }

    /// <summary>
    /// Timestamp of soft deletion. Managed automatically by Marten.
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    // ── Event Sourcing: Self-Aggregating Snapshot ─────────────────────

    /// <summary>
    /// Creates a new PageDocument from a PageCreated event.
    /// The service layer computes Path and Depth before calling this.
    /// </summary>
    public static PageDocument Create(PageCreated e) => new()
    {
        SiteId = e.SiteId,
        Title = e.Title,
        Slug = e.Slug,
        ParentId = e.ParentId,
        Order = e.Order,
        PublicationState = ContentPublicationState.Draft
    };

    public void Apply(PageContentUpdated e)
    {
        Title = e.Title;
        Slug = e.Slug;
        Summary = e.Summary;
        SeoTitle = e.SeoTitle;
        SeoDescription = e.SeoDescription;
        if (e.LayoutRegions is not null) LayoutRegions = e.LayoutRegions.ToList();
        if (e.Blocks is not null) Blocks = e.Blocks.ToList();
        ModifiedOn = DateTimeOffset.UtcNow;
    }

    public void Apply(PagePublished _)
    {
        PublicationState = ContentPublicationState.Published;
        PublishedOn = DateTimeOffset.UtcNow;
    }

    public void Apply(PageArchived _) =>
        PublicationState = ContentPublicationState.Archived;

    public void Apply(PageStateChanged e)
    {
        PublicationState = e.NewState;
        if (e.NewState == ContentPublicationState.Published)
            PublishedOn = DateTimeOffset.UtcNow;
    }

    public void Apply(PageDeleted _) =>
        Deleted = true;

    public void Apply(PageRestored _) =>
        Deleted = false;

    public void Apply(PageMoved e)
    {
        ParentId = e.NewParentId;
        Path = e.NewPath;
        Depth = e.NewDepth;
        Order = e.NewOrder;
    }

    public void Apply(PageVisibilityChanged e) =>
        IsHidden = e.IsHidden;
}
