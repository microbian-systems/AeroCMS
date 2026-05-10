using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Layout;
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
    int Order);

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
    List<EditorBlock>? Blocks);

/// <summary>
/// Appended when the page is published. Sets PublicationState = Published.
/// </summary>
public sealed record PagePublished;

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
public sealed record PageVisibilityChanged(bool IsHidden);

/// <summary>
/// Appended when the publication state transitions during workflow
/// (e.g., Draft → InReview, Draft → Published, Published → Archived).
/// Covers all ContentPublicationState transitions.
/// </summary>
public sealed record PageStateChanged(ContentPublicationState NewState);
