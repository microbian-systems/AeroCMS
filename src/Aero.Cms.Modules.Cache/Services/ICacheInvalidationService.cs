using Aero.Cms.Abstractions.Events;

namespace Aero.Cms.Modules.Cache.Services;

public interface ICacheInvalidationService
{
    Task InvalidateContentAsync(ContentUpdatedEvent @event, CancellationToken cancellationToken = default);
    Task InvalidateNavigationAsync(NavigationMenuChangedEvent @event, CancellationToken cancellationToken = default);
    Task InvalidateFooterAsync(FooterChangedEvent @event, CancellationToken cancellationToken = default);
}
