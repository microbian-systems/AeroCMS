using Aero.Cms.Abstractions.Events;
using Aero.Cms.Modules.Cache.Services;
using Microsoft.Extensions.Logging;
using Wolverine.Attributes;

namespace Aero.Cms.Modules.Cache.Handlers;

[WolverineHandler]
public sealed class ContentUpdatedHandler(
    ICacheInvalidationService cacheInvalidationService,
    ILogger<ContentUpdatedHandler> logger)
{
    // Lean events (existing subscribers — keep for backward compat)
    public Task Handle(PageContentUpdatedEvent @event, CancellationToken cancellationToken)
        => cacheInvalidationService.InvalidateContentAsync(@event, cancellationToken);

    public Task Handle(BlogPostContentUpdatedEvent @event, CancellationToken cancellationToken)
        => cacheInvalidationService.InvalidateContentAsync(@event, cancellationToken);

    public Task Handle(DocsPageContentUpdatedEvent @event, CancellationToken cancellationToken)
        => cacheInvalidationService.InvalidateContentAsync(@event, cancellationToken);

    public Task Handle(NavigationMenuChangedEvent @event, CancellationToken cancellationToken)
        => cacheInvalidationService.InvalidateNavigationAsync(@event, cancellationToken);

    public Task Handle(FooterChangedEvent @event, CancellationToken cancellationToken)
        => cacheInvalidationService.InvalidateFooterAsync(@event, cancellationToken);

    // Rich events — carry PageViewModel for zero-DB consumers
    public Task Handle(PageViewModelCreated @event, CancellationToken ct)
        => cacheInvalidationService.InvalidateContentAsync(
            new PageContentUpdatedEvent(@event.record.Id, @event.record.SiteId, @event.record.Slug ?? "", null), ct);

    public Task Handle(PageViewModelUpdated @event, CancellationToken ct)
        => cacheInvalidationService.InvalidateContentAsync(
            new PageContentUpdatedEvent(@event.record.Id, @event.record.SiteId, @event.record.Slug ?? "", null), ct);

    public Task Handle(PageViewModelDeleted @event, CancellationToken ct)
        => cacheInvalidationService.InvalidateContentAsync(
            new PageContentUpdatedEvent(@event.record.Id, @event.record.SiteId, @event.record.Slug ?? "", null), ct);

    // Content item events — invalidate public URL cache for the item
    public Task Handle(ContentItemViewModelCreated e, CancellationToken ct)
    {
        logger.LogInformation("ContentItem created: {Id} ({Slug}) in {TypeAlias}", e.record.Id, e.record.Slug, e.record.ContentTypeAlias);
        return cacheInvalidationService.InvalidateContentAsync(
            new ContentItemUpdatedEvent(e.record.Id, e.record.SiteId, e.record.Slug ?? "", null), ct);
    }

    public Task Handle(ContentItemViewModelUpdated e, CancellationToken ct)
    {
        logger.LogInformation("ContentItem updated: {Id} ({Slug}) in {TypeAlias}", e.record.Id, e.record.Slug, e.record.ContentTypeAlias);
        return cacheInvalidationService.InvalidateContentAsync(
            new ContentItemUpdatedEvent(e.record.Id, e.record.SiteId, e.record.Slug ?? "", null), ct);
    }

    public Task Handle(ContentItemViewModelDeleted e, CancellationToken ct)
    {
        logger.LogInformation("ContentItem deleted: {Id} ({Slug}) in {TypeAlias}", e.record.Id, e.record.Slug, e.record.ContentTypeAlias);
        return cacheInvalidationService.InvalidateContentAsync(
            new ContentItemUpdatedEvent(e.record.Id, e.record.SiteId, e.record.Slug ?? "", null), ct);
    }

    // Content type events — log only (coarse invalidation needs tag design)
    public Task Handle(ContentTypeViewModelCreated e, CancellationToken ct)
    {
        logger.LogInformation("ContentType created: {Alias} ({Name}) for site {SiteId}",
            e.record.Alias, e.record.Name, e.record.SiteId);
        return Task.CompletedTask;
    }

    public Task Handle(ContentTypeViewModelUpdated e, CancellationToken ct)
    {
        logger.LogInformation("ContentType updated: {Alias} ({Name}) for site {SiteId}",
            e.record.Alias, e.record.Name, e.record.SiteId);
        return Task.CompletedTask;
    }

    public Task Handle(ContentTypeViewModelDeleted e, CancellationToken ct)
    {
        logger.LogInformation("ContentType deleted: {Alias} ({Name}) for site {SiteId}",
            e.record.Alias, e.record.Name, e.record.SiteId);
        return Task.CompletedTask;
    }
}
