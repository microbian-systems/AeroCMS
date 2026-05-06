using Aero.Cms.Abstractions.Events;
using Aero.Cms.Modules.Cache.Services;
using Wolverine.Attributes;

namespace Aero.Cms.Modules.Cache.Handlers;

[WolverineHandler]
public sealed class ContentUpdatedHandler(ICacheInvalidationService cacheInvalidationService)
{
    public Task Handle(PageContentUpdatedEvent @event, CancellationToken cancellationToken)
        => cacheInvalidationService.InvalidateContentAsync(@event, cancellationToken);

    public Task Handle(BlogPostContentUpdatedEvent @event, CancellationToken cancellationToken)
        => cacheInvalidationService.InvalidateContentAsync(@event, cancellationToken);

    public Task Handle(DocsPageContentUpdatedEvent @event, CancellationToken cancellationToken)
        => cacheInvalidationService.InvalidateContentAsync(@event, cancellationToken);
}
