using Aero.Models.Entities;
using Aero.Events;
using Aero.Core.Entities;
namespace Aero.Cms.Abstractions.Events;


/// <summary>
/// Event fired when a content's slug has been updated and published.
/// </summary>
public record SlugUpdated(
    long ContentId,
    string ContentType,
    string NewSlug,
    string? OldSlug = null) : AeroEvent($"{OldSlug}->{NewSlug}");

/// <summary>
/// Represents a record for PageContentUpdatedEvent.
/// </summary>
public sealed record PageContentUpdatedEvent(
    long ContentId,
    long SiteId,
    string NewSlug,
    string? OldSlug = null) : ContentUpdatedEvent(ContentId, SiteId, NewSlug, OldSlug, "page");

/// <summary>
/// Represents a record for BlogPostContentUpdatedEvent.
/// </summary>
public sealed record BlogPostContentUpdatedEvent(
    long ContentId,
    long SiteId,
    string NewSlug,
    string? OldSlug = null) : ContentUpdatedEvent(ContentId, SiteId, NewSlug, OldSlug, "blog");

/// <summary>
/// Represents a record for DocsPageContentUpdatedEvent.
/// </summary>
public sealed record DocsPageContentUpdatedEvent(
    long ContentId,
    long SiteId,
    string NewSlug,
    string? OldSlug = null) : ContentUpdatedEvent(ContentId, SiteId, NewSlug, OldSlug, "docs");

/// <summary>
/// Represents a record for ContentItemUpdatedEvent.
/// </summary>
public sealed record ContentItemUpdatedEvent(
    long ContentId,
    long SiteId,
    string NewSlug,
    string? OldSlug = null) : ContentUpdatedEvent(ContentId, SiteId, NewSlug, OldSlug, "content-item");

/// <summary>
/// Represents a record for NavigationMenuChangedEvent.
/// </summary>
public sealed record NavigationMenuChangedEvent(
    long NavMenuId,
    long SiteId,
    NavigationMenuChangeKind ChangeKind,
    DateTimeOffset ChangedOn) : AeroEvent($"navigation menu {NavMenuId} {ChangeKind} for site {SiteId}");

/// <summary>
/// Defines an enumeration for NavigationMenuChangeKind.
/// </summary>
public enum NavigationMenuChangeKind
{
    Published,
    DefaultChanged,
    Archived
}

/// <summary>
/// Represents a record for FooterChangedEvent.
/// </summary>
public sealed record FooterChangedEvent(
    long FooterId,
    long SiteId,
    FooterChangeKind ChangeKind,
    DateTimeOffset ChangedOn) : AeroEvent($"footer {FooterId} {ChangeKind} for site {SiteId}");

/// <summary>
/// Defines an enumeration for FooterChangeKind.
/// </summary>
public enum FooterChangeKind
{
    Published,
    DefaultChanged,
    Archived
}

/// <summary>
/// Represents a record for ContentUpdatedEvent.
/// </summary>
public abstract record ContentUpdatedEvent<T>(
    T document,
    string NewSlug,
    string? OldSlug) : AeroEvent<T>(document, $"{typeof(T)} content updated for site {document.Id}: {OldSlug}->{NewSlug}")
    where T : IEntity
    ;

/// <summary>
/// Represents a record for ContentUpdatedEvent.
/// </summary>
public abstract record ContentUpdatedEvent(
    long ContentId,
    long SiteId,
    string NewSlug,
    string? OldSlug,
    string ContentType) : AeroEvent($"{ContentType} content updated for site {SiteId}: {OldSlug}->{NewSlug}");


/// <summary>
/// Represents a record for AeroEvent.
/// </summary>
public abstract record AeroEvent<T>(T record, string? msg = null) : AeroEvent(msg!);


// ── Flattened event types (formerly nested in AeroEvent<T>) ──────────
// Wolverine's GetPrettyName can't parse nested generic type names, so all
// AeroEvent<T>.* types are now top-level records.

// alias events
/// <summary>
/// Represents a record for AliasViewModelCreated.
/// </summary>
public sealed record AliasViewModelCreated(AliasViewModel alias, string? msg = null) : AeroEvent<AliasViewModel>(alias, msg);
/// <summary>
/// Represents a record for AliasViewModelUpdated.
/// </summary>
public sealed record AliasViewModelUpdated(AliasViewModel alias, string? msg = null) : AeroEvent<AliasViewModel>(alias, msg);
/// <summary>
/// Represents a record for AliasViewModelDeleted.
/// </summary>
public sealed record AliasViewModelDeleted(AliasViewModel alias, string? msg = null) : AeroEvent<AliasViewModel>(alias, msg);

// category events
/// <summary>
/// Represents a record for CategoryViewModelCreated.
/// </summary>
public sealed record CategoryViewModelCreated(CategoryViewModel category, string? msg = null) : AeroEvent<CategoryViewModel>(category, msg);
/// <summary>
/// Represents a record for CategoryViewModelUpdated.
/// </summary>
public sealed record CategoryViewModelUpdated(CategoryViewModel category, string? msg = null) : AeroEvent<CategoryViewModel>(category, msg);
/// <summary>
/// Represents a record for CategoryViewModelDeleted.
/// </summary>
public sealed record CategoryViewModelDeleted(CategoryViewModel category, string? msg = null) : AeroEvent<CategoryViewModel>(category, msg);

// doc events
/// <summary>
/// Represents a record for DocViewModelCreated.
/// </summary>
public sealed record DocViewModelCreated(DocViewModel doc, string? msg = null) : AeroEvent<DocViewModel>(doc, msg);
/// <summary>
/// Represents a record for DocViewModelUpdated.
/// </summary>
public sealed record DocViewModelUpdated(DocViewModel doc, string? msg = null) : AeroEvent<DocViewModel>(doc, msg);
/// <summary>
/// Represents a record for DocViewModelDeleted.
/// </summary>
public sealed record DocViewModelDeleted(DocViewModel doc, string? msg = null) : AeroEvent<DocViewModel>(doc, msg);

// media events
/// <summary>
/// Represents a record for MediaViewModelCreated.
/// </summary>
public sealed record MediaViewModelCreated(MediaViewModel media, string? msg = null) : AeroEvent<MediaViewModel>(media, msg);
/// <summary>
/// Represents a record for MediaViewModelUpdated.
/// </summary>
public sealed record MediaViewModelUpdated(MediaViewModel media, string? msg = null) : AeroEvent<MediaViewModel>(media, msg);
/// <summary>
/// Represents a record for MediaViewModelDeleted.
/// </summary>
public sealed record MediaViewModelDeleted(MediaViewModel media, string? msg = null) : AeroEvent<MediaViewModel>(media, msg);

// page events
/// <summary>
/// Represents a record for PageViewModelCreated.
/// </summary>
public sealed record PageViewModelCreated(PageViewModel page, string? msg = null) : AeroEvent<PageViewModel>(page, msg);
/// <summary>
/// Represents a record for PageViewModelUpdated.
/// </summary>
public sealed record PageViewModelUpdated(PageViewModel page, string? msg = null) : AeroEvent<PageViewModel>(page, msg);
/// <summary>
/// Represents a record for PageViewModelDeleted.
/// </summary>
public sealed record PageViewModelDeleted(PageViewModel page, string? msg = null) : AeroEvent<PageViewModel>(page, msg);

// post events
/// <summary>
/// Represents a record for PostViewModelCreated.
/// </summary>
public sealed record PostViewModelCreated(PostViewModel post, string? msg = null) : AeroEvent<PostViewModel>(post, msg);
/// <summary>
/// Represents a record for PostViewModelUpdated.
/// </summary>
public sealed record PostViewModelUpdated(PostViewModel post, string? msg = null) : AeroEvent<PostViewModel>(post, msg);
/// <summary>
/// Represents a record for PostViewModelDeleted.
/// </summary>
public sealed record PostViewModelDeleted(PostViewModel post, string? msg = null) : AeroEvent<PostViewModel>(post, msg);

// settings events
/// <summary>
/// Represents a record for SettingsViewModelUpdated.
/// </summary>
public sealed record SettingsViewModelUpdated(SettingsViewModel settings, string? msg = null) : AeroEvent<SettingsViewModel>(settings, msg);

// site events
/// <summary>
/// Represents a record for SiteViewModelCreated.
/// </summary>
public sealed record SiteViewModelCreated(SiteViewModel site, string? msg = null) : AeroEvent<SiteViewModel>(site, msg);
/// <summary>
/// Represents a record for SiteViewModelUpdated.
/// </summary>
public sealed record SiteViewModelUpdated(SiteViewModel site, string? msg = null) : AeroEvent<SiteViewModel>(site, msg);
/// <summary>
/// Represents a record for SiteViewModelDeleted.
/// </summary>
public sealed record SiteViewModelDeleted(SiteViewModel site, string? msg = null) : AeroEvent<SiteViewModel>(site, msg);

/// <summary>
/// Published after a site's style profile has been persisted.
/// </summary>
public sealed record SiteStyleProfileChangedEvent(
    long SiteId,
    long Revision,
    DateTimeOffset ChangedOn) : AeroEvent($"style profile revision {Revision} changed for site {SiteId}");

// tag events
/// <summary>
/// Represents a record for TagViewModelCreated.
/// </summary>
public sealed record TagViewModelCreated(TagViewModel tag, string? msg = null) : AeroEvent<TagViewModel>(tag, msg);
/// <summary>
/// Represents a record for TagViewModelUpdated.
/// </summary>
public sealed record TagViewModelUpdated(TagViewModel tag, string? msg = null) : AeroEvent<TagViewModel>(tag, msg);
/// <summary>
/// Represents a record for TagViewModelDeleted.
/// </summary>
public sealed record TagViewModelDeleted(TagViewModel tag, string? msg = null) : AeroEvent<TagViewModel>(tag, msg);

// user events
/// <summary>
/// Represents a record for UserViewModelCreated.
/// </summary>
public sealed record UserViewModelCreated(AeroUser user, string? msg = null) : AeroEvent<AeroUser>(user, msg);
/// <summary>
/// Represents a record for UserViewModelUpdated.
/// </summary>
public sealed record UserViewModelUpdated(AeroUser user, string? msg = null) : AeroEvent<AeroUser>(user, msg);
/// <summary>
/// Represents a record for UserViewModelDeleted.
/// </summary>
public sealed record UserViewModelDeleted(AeroUser user, string? msg = null) : AeroEvent<AeroUser>(user, msg);

// content type events
/// <summary>
/// Represents a record for ContentTypeViewModelCreated.
/// </summary>
public sealed record ContentTypeViewModelCreated(ContentTypeViewModel contentType, string? msg = null) : AeroEvent<ContentTypeViewModel>(contentType, msg);
/// <summary>
/// Represents a record for ContentTypeViewModelUpdated.
/// </summary>
public sealed record ContentTypeViewModelUpdated(ContentTypeViewModel contentType, string? msg = null) : AeroEvent<ContentTypeViewModel>(contentType, msg);
/// <summary>
/// Represents a record for ContentTypeViewModelDeleted.
/// </summary>
public sealed record ContentTypeViewModelDeleted(ContentTypeViewModel contentType, string? msg = null) : AeroEvent<ContentTypeViewModel>(contentType, msg);

// content item events
/// <summary>
/// Represents a record for ContentItemViewModelCreated.
/// </summary>
public sealed record ContentItemViewModelCreated(ContentItemViewModel contentItem, string? msg = null) : AeroEvent<ContentItemViewModel>(contentItem, msg);
/// <summary>
/// Represents a record for ContentItemViewModelUpdated.
/// </summary>
public sealed record ContentItemViewModelUpdated(ContentItemViewModel contentItem, string? msg = null) : AeroEvent<ContentItemViewModel>(contentItem, msg);
/// <summary>
/// Represents a record for ContentItemViewModelDeleted.
/// </summary>
public sealed record ContentItemViewModelDeleted(ContentItemViewModel contentItem, string? msg = null) : AeroEvent<ContentItemViewModel>(contentItem, msg);
