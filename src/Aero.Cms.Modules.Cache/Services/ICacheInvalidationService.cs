using Aero.Cms.Abstractions.Events;

namespace Aero.Cms.Modules.Cache.Services;

/// <summary>
/// Invalidates cached CMS content and rendered output in response to published change events.
/// </summary>
/// <remarks>
/// Implementations may target multiple cache layers. The contract does not promise synchronous cross-node
/// coherence, retries, transactional coupling to the triggering event, or that an event subscription invokes it.
/// Cancellation and failures are implementation-defined; callers must not treat successful task completion as a
/// distributed-consistency guarantee.
/// </remarks>
public interface ICacheInvalidationService
{
    /// <summary>Invalidates cache entries associated with a changed content item.</summary>
    /// <param name="event">The event identifying the content, its site, current slug, and optional prior slug.</param>
    /// <param name="cancellationToken">Token forwarded to cache eviction operations.</param>
Task InvalidateContentAsync(ContentUpdatedEvent @event, CancellationToken cancellationToken = default);
    /// <summary>Invalidates rendered pages affected by a navigation-menu change.</summary>
    /// <param name="event">The navigation change event.</param>
    /// <param name="cancellationToken">Token forwarded to cache eviction operations.</param>
Task InvalidateNavigationAsync(NavigationMenuChangedEvent @event, CancellationToken cancellationToken = default);
    /// <summary>Invalidates rendered pages affected by a footer change.</summary>
    /// <param name="event">The footer change event.</param>
    /// <param name="cancellationToken">Token forwarded to cache eviction operations.</param>
Task InvalidateFooterAsync(FooterChangedEvent @event, CancellationToken cancellationToken = default);
        /// <summary>
    /// Invalidates rendered page responses whose CSS depends on a site's style profile.
    /// </summary>
    /// <param name="event">The style-profile revision event identifying the affected site.</param>
    /// <param name="cancellationToken">Token forwarded to output-cache eviction.</param>
Task InvalidateSiteStyleProfileAsync(
    SiteStyleProfileChangedEvent @event,
    CancellationToken cancellationToken = default);
        /// <summary>Invalidates rendered pages whose stylesheet selection changed.</summary>
Task InvalidateSiteThemeAsync(
    SiteThemeChangedEvent @event,
    CancellationToken cancellationToken = default);
}
