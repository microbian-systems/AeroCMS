using Aero.Cms.Abstractions.Events;
using Aero.Cms.Modules.Cache.Services;
using Microsoft.Extensions.Logging;
using Wolverine.Attributes;

namespace Aero.Cms.Modules.Cache.Handlers;

/// <summary>
/// Wolverine event handler that forwards supported content-change events to cache invalidation.
/// </summary>
/// <remarks>
/// Forwarded calls preserve the dispatcher cancellation token and let invalidation failures propagate. Content-type
/// view-model events only log and deliberately perform no eviction until a coarse-tag design exists.
/// </remarks>
[WolverineHandler]
public sealed class ContentUpdatedHandler(
    ICacheInvalidationService cacheInvalidationService,
    ILogger<ContentUpdatedHandler> logger)
{
    // Lean events (existing subscribers — keep for backward compat)
        /// <summary>
    /// Forwards a page content change to content invalidation.
    /// </summary>
public Task Handle(PageContentUpdatedEvent @event, CancellationToken cancellationToken)
        => cacheInvalidationService.InvalidateContentAsync(@event, cancellationToken);

        /// <summary>
    /// Forwards a blog content change to content invalidation.
    /// </summary>
public Task Handle(BlogPostContentUpdatedEvent @event, CancellationToken cancellationToken)
        => cacheInvalidationService.InvalidateContentAsync(@event, cancellationToken);

        /// <summary>
    /// Forwards a documentation-page content change to content invalidation.
    /// </summary>
public Task Handle(DocsPageContentUpdatedEvent @event, CancellationToken cancellationToken)
        => cacheInvalidationService.InvalidateContentAsync(@event, cancellationToken);

        /// <summary>
    /// Forwards a navigation change to coarse cache invalidation.
    /// </summary>
public Task Handle(NavigationMenuChangedEvent @event, CancellationToken cancellationToken)
        => cacheInvalidationService.InvalidateNavigationAsync(@event, cancellationToken);

        /// <summary>
    /// Forwards a footer change to coarse cache invalidation.
    /// </summary>
public Task Handle(FooterChangedEvent @event, CancellationToken cancellationToken)
        => cacheInvalidationService.InvalidateFooterAsync(@event, cancellationToken);

        /// <summary>
    /// Invalidates all rendered pages for a site after its design tokens change.
    /// </summary>
public Task Handle(SiteStyleProfileChangedEvent @event, CancellationToken cancellationToken)
        => cacheInvalidationService.InvalidateSiteStyleProfileAsync(@event, cancellationToken);

    // Rich events — carry PageViewModel for zero-DB consumers
        /// <summary>
    /// Forwards a rich page-create event as a current-slug content change.
    /// </summary>
public Task Handle(PageViewModelCreated @event, CancellationToken ct)
        => cacheInvalidationService.InvalidateContentAsync(
            new PageContentUpdatedEvent(@event.record.Id, @event.record.SiteId, @event.record.Slug ?? "", null), ct);

        /// <summary>
    /// Forwards a rich page-update event as a current-slug content change.
    /// </summary>
public Task Handle(PageViewModelUpdated @event, CancellationToken ct)
        => cacheInvalidationService.InvalidateContentAsync(
            new PageContentUpdatedEvent(@event.record.Id, @event.record.SiteId, @event.record.Slug ?? "", null), ct);

        /// <summary>
    /// Forwards a rich page-delete event as a current-slug content change.
    /// </summary>
public Task Handle(PageViewModelDeleted @event, CancellationToken ct)
        => cacheInvalidationService.InvalidateContentAsync(
            new PageContentUpdatedEvent(@event.record.Id, @event.record.SiteId, @event.record.Slug ?? "", null), ct);

    // Content item events — invalidate public URL cache for the item
        /// <summary>
    /// Logs and invalidates the current-slug content-item representation.
    /// </summary>
public Task Handle(ContentItemViewModelCreated e, CancellationToken ct)
    {
        logger.LogInformation("ContentItem created: {Id} ({Slug}) in {TypeAlias}", e.record.Id, e.record.Slug, e.record.ContentTypeAlias);
        return cacheInvalidationService.InvalidateContentAsync(
            new ContentItemUpdatedEvent(e.record.Id, e.record.SiteId, e.record.Slug ?? "", null), ct);
    }

        /// <summary>
    /// Logs and invalidates the current-slug content-item representation.
    /// </summary>
public Task Handle(ContentItemViewModelUpdated e, CancellationToken ct)
    {
        logger.LogInformation("ContentItem updated: {Id} ({Slug}) in {TypeAlias}", e.record.Id, e.record.Slug, e.record.ContentTypeAlias);
        return cacheInvalidationService.InvalidateContentAsync(
            new ContentItemUpdatedEvent(e.record.Id, e.record.SiteId, e.record.Slug ?? "", null), ct);
    }

        /// <summary>
    /// Logs and invalidates the current-slug content-item representation.
    /// </summary>
public Task Handle(ContentItemViewModelDeleted e, CancellationToken ct)
    {
        logger.LogInformation("ContentItem deleted: {Id} ({Slug}) in {TypeAlias}", e.record.Id, e.record.Slug, e.record.ContentTypeAlias);
        return cacheInvalidationService.InvalidateContentAsync(
            new ContentItemUpdatedEvent(e.record.Id, e.record.SiteId, e.record.Slug ?? "", null), ct);
    }

    // Content type events — log only (coarse invalidation needs tag design)
        /// <summary>
    /// Logs the content-type creation without evicting cache entries.
    /// </summary>
public Task Handle(ContentTypeViewModelCreated e, CancellationToken ct)
    {
        logger.LogInformation("ContentType created: {Alias} ({Name}) for site {SiteId}",
            e.record.Alias, e.record.Name, e.record.SiteId);
        return Task.CompletedTask;
    }

        /// <summary>
    /// Logs the content-type update without evicting cache entries.
    /// </summary>
public Task Handle(ContentTypeViewModelUpdated e, CancellationToken ct)
    {
        logger.LogInformation("ContentType updated: {Alias} ({Name}) for site {SiteId}",
            e.record.Alias, e.record.Name, e.record.SiteId);
        return Task.CompletedTask;
    }

        /// <summary>
    /// Logs the content-type deletion without evicting cache entries.
    /// </summary>
public Task Handle(ContentTypeViewModelDeleted e, CancellationToken ct)
    {
        logger.LogInformation("ContentType deleted: {Alias} ({Name}) for site {SiteId}",
            e.record.Alias, e.record.Name, e.record.SiteId);
        return Task.CompletedTask;
    }
}
