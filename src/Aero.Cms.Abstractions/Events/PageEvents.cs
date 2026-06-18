using Aero.Cms.Abstractions.Blocks.Layout;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Enums;

namespace Aero.Cms.Abstractions.Events;

/// <summary>
/// Appended when a new page is created. The service layer computes
/// Path and Depth before appending this event.
/// </summary>
public sealed record PageCreated(
    long SiteId,
    string Title,
    string Slug,
    long? ParentId,
    int Order,
    string Path = "",
    int Depth = 0,
    ContentPublicationState PublicationState = ContentPublicationState.Draft,
    PageKind Kind = PageKind.Standard,
    string Culture = "en-US",
    long? TranslationGroupId = null);

/// <summary>
/// Appended when content fields change: Title, Slug, LayoutRegions,
/// Blocks, Summary, SEO fields.
/// </summary>
public sealed record PageContentUpdated(
    string Title,
    string Slug,
    string? Summary,
    string? SeoTitle,
    string? SeoDescription,
    List<LayoutRegion>? LayoutRegions,
    List<NeoPageNode>? RootNodes = null,
    PageKind Kind = PageKind.Standard,
    bool ShowHeaderNavigation = true,
    string? HeaderImageUrl = null,
    bool HideHeader = false,
    bool HideFooter = false,
    bool ShowChatAgent = true,
    Dictionary<string, long>? BlockIdMap = null);

/// <summary>
/// Appended when the editor saves a draft page composition. AeroCMS already
/// uses Marten event sourcing for pages; this event is the page-tree successor
/// to the legacy <see cref="PageContentUpdated"/> body snapshot.
/// </summary>
/// <remarks>
/// The persisted event is intentionally coarse grained. Canvas-level undo/redo
/// remains an editor concern, while Marten records durable save milestones that
/// can project the page document, flattened node indexes, search documents, and
/// component usage indexes.
/// </remarks>
public sealed record PageCompositionDraftSaved(
    long PageId,
    long SiteId,
    long CompositionId,
    string Culture,
    long ContentRevision,
    string Title,
    string Slug,
    string? Summary,
    string? SeoTitle,
    string? SeoDescription,
    List<NeoPageNode> RootNodes,
    List<LayoutRegion>? LayoutRegions = null,
    PageKind Kind = PageKind.Standard,
    bool ShowHeaderNavigation = true,
    string? HeaderImageUrl = null,
    bool HideHeader = false,
    bool HideFooter = false,
    bool ShowChatAgent = true,
    Dictionary<string, long>? BlockIdMap = null);

/// <summary>
/// Appended when a page composition is published. This is the page-tree
/// equivalent of the legacy <see cref="PagePublished"/> event and should be the
/// durable source for published composition projections.
/// </summary>
public sealed record PageCompositionPublished(
    long PageId,
    long SiteId,
    long PublishedCompositionId,
    long PublishedVersion,
    string Culture,
    string Title,
    string Slug,
    string? Summary,
    string? SeoTitle,
    string? SeoDescription,
    List<NeoPageNode> RootNodes,
    List<LayoutRegion>? LayoutRegions = null,
    PageKind Kind = PageKind.Standard,
    bool ShowHeaderNavigation = true,
    string? HeaderImageUrl = null,
    bool HideHeader = false,
    bool HideFooter = false,
    bool ShowChatAgent = true,
    Dictionary<string, long>? BlockIdMap = null);

/// <summary>
/// Appended when the page is published. Carries the computed version
/// and the layout manifest built by <c>IPageLayoutManifestBuilder</c>.
/// </summary>
/// <param name="PageId">The page being published.</param>
/// <param name="Version">Monotonic publish version (PublishedVersion + 1).</param>
/// <param name="LayoutRegions">The built layout manifest. Written to PageDocument.LayoutRegions.</param>
public sealed record PagePublished(
    long PageId = 0,
    long Version = 0,
    List<LayoutRegion>? LayoutRegions = null);

/// <summary>
/// Appended when page metadata is saved during draft editing.
/// Carries metadata only — no block/body content, no LayoutRegions.
/// Replaces the old <c>PageContentUpdated</c> for metadata-only saves.
/// </summary>
/// <param name="PageId">The page whose metadata changed.</param>
/// <param name="SiteId">The site the page belongs to.</param>
/// <param name="Title">New page title.</param>
/// <param name="Slug">New page slug.</param>
/// <param name="OldSlug">Previous slug, populated only when the slug changed (for cache eviction).</param>
/// <param name="Summary">Optional page summary.</param>
/// <param name="SeoTitle">Optional SEO title.</param>
/// <param name="SeoDescription">Optional SEO description.</param>
/// <param name="Kind">Page kind.</param>
/// <param name="ShowHeaderNavigation">Whether the global header nav is shown.</param>
/// <param name="HeaderImageUrl">Optional header background image URL.</param>
/// <param name="HideHeader">Hide the page header.</param>
/// <param name="HideFooter">Hide the page footer.</param>
/// <param name="ShowChatAgent">Show the chat agent widget.</param>
public sealed record PageMetadataUpdated(
    long PageId,
    long SiteId,
    string Title,
    string Slug,
    string? OldSlug,
    string? Summary,
    string? SeoTitle,
    string? SeoDescription,
    PageKind Kind = PageKind.Standard,
    bool ShowHeaderNavigation = true,
    string? HeaderImageUrl = null,
    bool HideHeader = false,
    bool HideFooter = false,
    bool ShowChatAgent = true);

/// <summary>
/// Appended when the page is archived. Sets PublicationState = Archived.
/// </summary>
public sealed record PageArchived;

/// <summary>
/// Appended when the page is soft-deleted. Marten auto-manages
/// the ISoftDeleted fields.
/// </summary>
/// <param name="Reason">Optional reason for deletion.</param>
public sealed record PageDeleted(string? Reason);

/// <summary>
/// Appended when a soft-deleted page is restored.
/// </summary>
public sealed record PageRestored;

/// <summary>
/// Appended when the page moves in the hierarchy (parent change,
/// reorder, or path changes from a Move operation).
/// </summary>
public sealed record PageMoved(
    long? NewParentId,
    string NewPath,
    int NewDepth,
    int NewOrder);

/// <summary>
/// Appended when the page's visibility changes.
/// </summary>
public sealed record PageVisibilityChanged(bool IsHidden, bool ShowInNavMenu);

/// <summary>
/// Appended when the publication state transitions during workflow
/// (e.g., Draft → InReview, Draft → Published, Published → Archived).
/// Covers all ContentPublicationState transitions.
/// </summary>
public sealed record PageStateChanged(ContentPublicationState NewState);
