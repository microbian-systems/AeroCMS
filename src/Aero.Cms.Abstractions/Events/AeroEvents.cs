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

public sealed record PageContentUpdatedEvent(
    long ContentId,
    long SiteId,
    string NewSlug,
    string? OldSlug = null) : ContentUpdatedEvent(ContentId, SiteId, NewSlug, OldSlug, "page");

public sealed record BlogPostContentUpdatedEvent(
    long ContentId,
    long SiteId,
    string NewSlug,
    string? OldSlug = null) : ContentUpdatedEvent(ContentId, SiteId, NewSlug, OldSlug, "blog");

public sealed record DocsPageContentUpdatedEvent(
    long ContentId,
    long SiteId,
    string NewSlug,
    string? OldSlug = null) : ContentUpdatedEvent(ContentId, SiteId, NewSlug, OldSlug, "docs");

public sealed record ContentItemUpdatedEvent(
    long ContentId,
    long SiteId,
    string NewSlug,
    string? OldSlug = null) : ContentUpdatedEvent(ContentId, SiteId, NewSlug, OldSlug, "content-item");

public sealed record NavigationMenuChangedEvent(
    long NavMenuId,
    long SiteId,
    NavigationMenuChangeKind ChangeKind,
    DateTimeOffset ChangedOn) : AeroEvent($"navigation menu {NavMenuId} {ChangeKind} for site {SiteId}");

public enum NavigationMenuChangeKind
{
    Published,
    DefaultChanged,
    Archived
}

public sealed record FooterChangedEvent(
    long FooterId,
    long SiteId,
    FooterChangeKind ChangeKind,
    DateTimeOffset ChangedOn) : AeroEvent($"footer {FooterId} {ChangeKind} for site {SiteId}");

public enum FooterChangeKind
{
    Published,
    DefaultChanged,
    Archived
}

public abstract record ContentUpdatedEvent<T>(
    T document,
    string NewSlug,
    string? OldSlug) : AeroEvent<T>(document, $"{typeof(T)} content updated for site {document.Id}: {OldSlug}->{NewSlug}")
    where T : IEntity
    ;

public abstract record ContentUpdatedEvent(
    long ContentId,
    long SiteId,
    string NewSlug,
    string? OldSlug,
    string ContentType) : AeroEvent($"{ContentType} content updated for site {SiteId}: {OldSlug}->{NewSlug}");


public abstract record AeroEvent<T>(T record, string? msg = null) : AeroEvent(msg!);


// ── Flattened event types (formerly nested in AeroEvent<T>) ──────────
// Wolverine's GetPrettyName can't parse nested generic type names, so all
// AeroEvent<T>.* types are now top-level records.

// alias events
public sealed record AliasViewModelCreated(AliasViewModel alias, string? msg = null) : AeroEvent<AliasViewModel>(alias, msg);
public sealed record AliasViewModelUpdated(AliasViewModel alias, string? msg = null) : AeroEvent<AliasViewModel>(alias, msg);
public sealed record AliasViewModelDeleted(AliasViewModel alias, string? msg = null) : AeroEvent<AliasViewModel>(alias, msg);

// category events
public sealed record CategoryViewModelCreated(CategoryViewModel category, string? msg = null) : AeroEvent<CategoryViewModel>(category, msg);
public sealed record CategoryViewModelUpdated(CategoryViewModel category, string? msg = null) : AeroEvent<CategoryViewModel>(category, msg);
public sealed record CategoryViewModelDeleted(CategoryViewModel category, string? msg = null) : AeroEvent<CategoryViewModel>(category, msg);

// doc events
public sealed record DocViewModelCreated(DocViewModel doc, string? msg = null) : AeroEvent<DocViewModel>(doc, msg);
public sealed record DocViewModelUpdated(DocViewModel doc, string? msg = null) : AeroEvent<DocViewModel>(doc, msg);
public sealed record DocViewModelDeleted(DocViewModel doc, string? msg = null) : AeroEvent<DocViewModel>(doc, msg);

// media events
public sealed record MediaViewModelCreated(MediaViewModel media, string? msg = null) : AeroEvent<MediaViewModel>(media, msg);
public sealed record MediaViewModelUpdated(MediaViewModel media, string? msg = null) : AeroEvent<MediaViewModel>(media, msg);
public sealed record MediaViewModelDeleted(MediaViewModel media, string? msg = null) : AeroEvent<MediaViewModel>(media, msg);

// page events
public sealed record PageViewModelCreated(PageViewModel page, string? msg = null) : AeroEvent<PageViewModel>(page, msg);
public sealed record PageViewModelUpdated(PageViewModel page, string? msg = null) : AeroEvent<PageViewModel>(page, msg);
public sealed record PageViewModelDeleted(PageViewModel page, string? msg = null) : AeroEvent<PageViewModel>(page, msg);

// post events
public sealed record PostViewModelCreated(PostViewModel post, string? msg = null) : AeroEvent<PostViewModel>(post, msg);
public sealed record PostViewModelUpdated(PostViewModel post, string? msg = null) : AeroEvent<PostViewModel>(post, msg);
public sealed record PostViewModelDeleted(PostViewModel post, string? msg = null) : AeroEvent<PostViewModel>(post, msg);

// settings events
public sealed record SettingsViewModelUpdated(SettingsViewModel settings, string? msg = null) : AeroEvent<SettingsViewModel>(settings, msg);

// site events
public sealed record SiteViewModelCreated(SiteViewModel site, string? msg = null) : AeroEvent<SiteViewModel>(site, msg);
public sealed record SiteViewModelUpdated(SiteViewModel site, string? msg = null) : AeroEvent<SiteViewModel>(site, msg);
public sealed record SiteViewModelDeleted(SiteViewModel site, string? msg = null) : AeroEvent<SiteViewModel>(site, msg);

// tag events
public sealed record TagViewModelCreated(TagViewModel tag, string? msg = null) : AeroEvent<TagViewModel>(tag, msg);
public sealed record TagViewModelUpdated(TagViewModel tag, string? msg = null) : AeroEvent<TagViewModel>(tag, msg);
public sealed record TagViewModelDeleted(TagViewModel tag, string? msg = null) : AeroEvent<TagViewModel>(tag, msg);

// user events
public sealed record UserViewModelCreated(AeroUser user, string? msg = null) : AeroEvent<AeroUser>(user, msg);
public sealed record UserViewModelUpdated(AeroUser user, string? msg = null) : AeroEvent<AeroUser>(user, msg);
public sealed record UserViewModelDeleted(AeroUser user, string? msg = null) : AeroEvent<AeroUser>(user, msg);

// content type events
public sealed record ContentTypeViewModelCreated(ContentTypeViewModel contentType, string? msg = null) : AeroEvent<ContentTypeViewModel>(contentType, msg);
public sealed record ContentTypeViewModelUpdated(ContentTypeViewModel contentType, string? msg = null) : AeroEvent<ContentTypeViewModel>(contentType, msg);
public sealed record ContentTypeViewModelDeleted(ContentTypeViewModel contentType, string? msg = null) : AeroEvent<ContentTypeViewModel>(contentType, msg);

// content item events
public sealed record ContentItemViewModelCreated(ContentItemViewModel contentItem, string? msg = null) : AeroEvent<ContentItemViewModel>(contentItem, msg);
public sealed record ContentItemViewModelUpdated(ContentItemViewModel contentItem, string? msg = null) : AeroEvent<ContentItemViewModel>(contentItem, msg);
public sealed record ContentItemViewModelDeleted(ContentItemViewModel contentItem, string? msg = null) : AeroEvent<ContentItemViewModel>(contentItem, msg);
