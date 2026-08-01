using Aero.Cms.Modules.Navigation.Domain;
using Aero.Cms.Modules.Navigation.Events;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Navigation.Projections;

/// <summary>
/// Projects navigation lifecycle events into queryable <see cref="NavMenuDocument"/> records.
/// </summary>
public sealed class NavMenuDocumentProjection : IProjection
{
    /// <summary>
    /// Gets the navigation event types consumed by this projection.
    /// </summary>
    public Type[] EventTypes =>
    [
        typeof(NavMenuCreated),
        typeof(NavMenuDraftSaved),
        typeof(NavMenuPublished),
        typeof(NavMenuArchived)
    ];

    /// <summary>
    /// Applies the projection engine's typed event batch asynchronously.
    /// </summary>
    /// <param name="context">The projection context and document session.</param>
    /// <param name="ct">The token used for aggregate loads.</param>
    /// <returns>A task that completes after affected documents have been staged.</returns>
    public Task ApplyAsync(IProjectionContext context, CancellationToken ct)
        => ApplyAsync(context.Session, context.TypedEvents, ct);

    /// <summary>
    /// Rebuilds and stages documents from complete navigation-stream event groups.
    /// </summary>
    /// <param name="operations">The document operation set receiving projected records.</param>
    /// <param name="events">The event batch; unrelated stream namespaces are ignored.</param>
    /// <remarks>
    /// This synchronous path begins each grouped aggregate at <see langword="null"/> and therefore
    /// expects the group's creation event to be present.
    /// </remarks>
public void Apply(IDocumentOperations operations, IReadOnlyList<IEvent> events)
    {
        foreach (var group in MenuEvents(events).GroupBy(e => e.StreamId.Value!))
        {
            ApplyStreamSync(operations, group);
        }
    }

    /// <summary>
    /// Loads existing documents, applies grouped navigation events in input order, and stages updates.
    /// </summary>
    /// <param name="operations">The query/session operation set receiving projected records.</param>
    /// <param name="events">The event batch; unrelated stream namespaces are ignored.</param>
    /// <param name="ct">The token used for aggregate loads.</param>
    /// <returns>A task that completes when all grouped documents have been staged.</returns>
public async Task ApplyAsync(IDocumentOperations operations, IReadOnlyList<IEvent> events, CancellationToken ct)
    {
        foreach (var group in MenuEvents(events).GroupBy(e => e.StreamId.Value!))
        {
            await ApplyStreamAsync(operations, group, ct);
        }
    }

    /// <summary>
    /// Filters an event batch to navigation-menu stream keys.
    /// </summary>
    /// <param name="events">The candidate events.</param>
    /// <returns>A deferred sequence of navigation-stream events.</returns>
    private static IEnumerable<IEvent> MenuEvents(IEnumerable<IEvent> events)
        => events.Where(e => NavMenuStreams.IsMenuStream(e.StreamId.Value));

    /// <summary>
    /// Replays one complete grouped stream without loading prior projection state.
    /// </summary>
    /// <param name="operations">The target document operations.</param>
    /// <param name="streamEvents">Events grouped by a valid menu stream key.</param>
    private static void ApplyStreamSync(IDocumentOperations operations, IGrouping<string, IEvent> streamEvents)
    {
        var id = NavMenuStreams.ExtractMenuId(streamEvents.Key);
        NavMenuDocument? aggregate = null;

        foreach (var @event in streamEvents)
        {
            aggregate = ApplyEvent(operations, aggregate, id, @event.Data);
        }

        if (aggregate is not null)
        {
            operations.Store(aggregate);
        }
    }

    /// <summary>
    /// Loads and incrementally updates one projected navigation document.
    /// </summary>
    /// <param name="operations">The target query/session operations.</param>
    /// <param name="streamEvents">Events grouped by a valid menu stream key.</param>
    /// <param name="ct">The token used to load existing state.</param>
    /// <returns>A task that completes after the projection is staged.</returns>
    private static async Task ApplyStreamAsync(IDocumentOperations operations, IGrouping<string, IEvent> streamEvents, CancellationToken ct)
    {
        var id = NavMenuStreams.ExtractMenuId(streamEvents.Key);
        var aggregate = await ((IQuerySession)operations).LoadAsync<NavMenuDocument>(id, ct);

        foreach (var @event in streamEvents)
        {
            aggregate = ApplyEvent(operations, aggregate, id, @event.Data);
        }

        if (aggregate is not null)
        {
            operations.Store(aggregate);
        }
    }

    /// <summary>
    /// Applies one recognized event to the current navigation projection.
    /// </summary>
    /// <param name="operations">Reserved projection operations; the current event handlers do not use it.</param>
    /// <param name="current">The current aggregate, if creation has already been observed.</param>
    /// <param name="id">The document identifier parsed from the stream key.</param>
    /// <param name="eventData">The event payload.</param>
    /// <returns>The created or updated aggregate; unknown events leave the current value unchanged.</returns>
    private static NavMenuDocument? ApplyEvent(
        IDocumentOperations operations,
        NavMenuDocument? current,
        long id,
        object eventData)
    {
        switch (eventData)
        {
            case NavMenuCreated e:
                return NavMenuDocument.Create(id, e);

            case NavMenuDraftSaved e:
                current?.Apply(e);
                return current;

            case NavMenuPublished e:
                current?.Apply(e);
                return current;

            case NavMenuArchived e:
                current?.Apply(e);
                return current;

            default:
                return current;
        }
    }
}
