using Aero.Cms.Abstractions.Events;

namespace Aero.Cms.Modules.Cache.Services;

public interface ICacheInvalidationService
{
    Task InvalidateContentAsync(ContentUpdatedEvent @event, CancellationToken cancellationToken = default);
}
