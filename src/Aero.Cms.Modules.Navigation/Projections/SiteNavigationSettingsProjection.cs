using Aero.Cms.Modules.Navigation.Domain;
using Aero.Cms.Modules.Navigation.Events;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Navigation.Projections;

/// <summary>
/// Projects site default-navigation events into <see cref="SiteNavigationSettingsDocument"/> records.
/// </summary>
public sealed class SiteNavigationSettingsProjection : IProjection
{
    /// <summary>
    /// Gets the default-navigation event type consumed by this projection.
    /// </summary>
    public Type[] EventTypes => [typeof(SiteDefaultNavMenuChanged)];

    /// <summary>
    /// Applies the projection engine's typed event batch asynchronously.
    /// </summary>
    /// <param name="context">The projection context and document session.</param>
    /// <param name="ct">The token used for aggregate loads.</param>
    /// <returns>A task that completes after affected settings documents have been staged.</returns>
    public Task ApplyAsync(IProjectionContext context, CancellationToken ct)
        => ApplyAsync(context.Session, context.TypedEvents, ct);

    /// <summary>
    /// Rebuilds and stages site settings from grouped default-navigation events.
    /// </summary>
    /// <param name="operations">The document operation set receiving projected records.</param>
    /// <param name="events">The event batch; unrelated stream namespaces are ignored.</param>
public void Apply(IDocumentOperations operations, IReadOnlyList<IEvent> events)
    {
        foreach (var group in SiteSettingsEvents(events).GroupBy(e => e.StreamId.Value!))
        {
            ApplyStreamSync(operations, group);
        }
    }

    /// <summary>
    /// Loads existing site settings and applies grouped changes in input order.
    /// </summary>
    /// <param name="operations">The query/session operation set receiving projected records.</param>
    /// <param name="events">The event batch; unrelated stream namespaces are ignored.</param>
    /// <param name="ct">The token used for aggregate loads.</param>
    /// <returns>A task that completes when all grouped settings documents have been staged.</returns>
public async Task ApplyAsync(IDocumentOperations operations, IReadOnlyList<IEvent> events, CancellationToken ct)
    {
        foreach (var group in SiteSettingsEvents(events).GroupBy(e => e.StreamId.Value!))
        {
            await ApplyStreamAsync(operations, group, ct);
        }
    }

    /// <summary>
    /// Filters an event batch to site-navigation-settings stream keys.
    /// </summary>
    /// <param name="events">The candidate events.</param>
    /// <returns>A deferred sequence of settings-stream events.</returns>
    private static IEnumerable<IEvent> SiteSettingsEvents(IEnumerable<IEvent> events)
        => events.Where(e => NavMenuStreams.IsSiteSettingsStream(e.StreamId.Value));

    /// <summary>
    /// Replays one grouped site-settings stream and stages the resulting document.
    /// </summary>
    /// <param name="operations">The target document operations.</param>
    /// <param name="streamEvents">Events grouped by a valid site-settings stream key.</param>
    private static void ApplyStreamSync(IDocumentOperations operations, IGrouping<string, IEvent> streamEvents)
    {
        var siteId = NavMenuStreams.ExtractSiteId(streamEvents.Key);
        SiteNavigationSettingsDocument? aggregate = null;

        foreach (var @event in streamEvents)
        {
            aggregate = ApplyEvent(aggregate, siteId, @event.Data);
        }

        if (aggregate is not null)
        {
            operations.Store(aggregate);
        }
    }

    /// <summary>
    /// Loads and incrementally updates one projected site-settings document.
    /// </summary>
    /// <param name="operations">The target query/session operations.</param>
    /// <param name="streamEvents">Events grouped by a valid site-settings stream key.</param>
    /// <param name="ct">The token used to load existing state.</param>
    /// <returns>A task that completes after the projection is staged.</returns>
    private static async Task ApplyStreamAsync(IDocumentOperations operations, IGrouping<string, IEvent> streamEvents, CancellationToken ct)
    {
        var siteId = NavMenuStreams.ExtractSiteId(streamEvents.Key);
        var aggregate = await ((IQuerySession)operations).LoadAsync<SiteNavigationSettingsDocument>(siteId, ct);

        foreach (var @event in streamEvents)
        {
            aggregate = ApplyEvent(aggregate, siteId, @event.Data);
        }

        if (aggregate is not null)
        {
            operations.Store(aggregate);
        }
    }

    /// <summary>
    /// Creates or updates a settings projection for one recognized event.
    /// </summary>
    /// <param name="current">The current settings document, if one exists.</param>
    /// <param name="siteId">The site identifier parsed from the stream key.</param>
    /// <param name="eventData">The event payload.</param>
    /// <returns>The created or updated document; unknown events leave the current value unchanged.</returns>
    private static SiteNavigationSettingsDocument? ApplyEvent(
        SiteNavigationSettingsDocument? current,
        long siteId,
        object eventData)
    {
        if (eventData is not SiteDefaultNavMenuChanged e)
        {
            return current;
        }

        if (current is null)
        {
            return SiteNavigationSettingsDocument.Create(siteId, e);
        }

        current.Apply(e);
        return current;
    }
}
