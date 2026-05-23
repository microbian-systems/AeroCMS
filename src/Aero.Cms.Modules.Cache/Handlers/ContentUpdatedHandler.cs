using Aero.Cms.Abstractions.Events;
using Aero.Cms.Modules.Cache.Services;
using Wolverine.Attributes;

namespace Aero.Cms.Modules.Cache.Handlers;

[WolverineHandler]
public sealed class ContentUpdatedHandler(ICacheInvalidationService cacheInvalidationService)
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
}
