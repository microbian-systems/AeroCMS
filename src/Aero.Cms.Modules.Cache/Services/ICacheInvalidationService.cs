using Aero.Cms.Abstractions.Events;

namespace Aero.Cms.Modules.Cache.Services;

/// <summary>
/// Defines an interface for ICacheInvalidationService.
/// </summary>
public interface ICacheInvalidationService
{
        /// <summary>
    /// InvalidateContentAsync method.
    /// </summary>
Task InvalidateContentAsync(ContentUpdatedEvent @event, CancellationToken cancellationToken = default);
        /// <summary>
    /// InvalidateNavigationAsync method.
    /// </summary>
Task InvalidateNavigationAsync(NavigationMenuChangedEvent @event, CancellationToken cancellationToken = default);
        /// <summary>
    /// InvalidateFooterAsync method.
    /// </summary>
Task InvalidateFooterAsync(FooterChangedEvent @event, CancellationToken cancellationToken = default);
        /// <summary>
    /// Invalidates all rendered page responses whose CSS depends on a site's style profile.
    /// </summary>
Task InvalidateSiteStyleProfileAsync(
    SiteStyleProfileChangedEvent @event,
    CancellationToken cancellationToken = default);
}
