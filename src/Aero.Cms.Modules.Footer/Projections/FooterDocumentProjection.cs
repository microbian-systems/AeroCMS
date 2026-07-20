using Aero.Cms.Modules.Footer.Domain;
using Aero.Cms.Modules.Footer.Events;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Footer.Projections;

/// <summary>
/// Projects footer lifecycle events into searchable <see cref="FooterDocument"/> metadata.
/// </summary>
public sealed class FooterDocumentProjection : IProjection
{
    /// <inheritdoc />
    public Type[] EventTypes =>
    [
        typeof(FooterCreated),
        typeof(FooterDraftSaved),
        typeof(FooterPublished),
        typeof(FooterArchived)
    ];

    /// <inheritdoc />
    public Task ApplyAsync(IProjectionContext context, CancellationToken ct)
        => ApplyAsync(context.Session, context.TypedEvents, ct);

    /// <summary>
    /// Applies complete event batches synchronously and stores one materialized document per footer stream.
    /// </summary>
    /// <param name="operations">The document session that receives projected documents.</param>
    /// <param name="events">The event batch to group by footer stream.</param>
    /// <remarks>
    /// This overload starts each aggregate from <see langword="null"/>. A grouped batch without a
    /// <see cref="FooterCreated"/> event therefore does not update an existing projection.
    /// Stream keys with the footer prefix but an invalid integer suffix cause an exception.
    /// </remarks>
    public void Apply(IDocumentOperations operations, IReadOnlyList<IEvent> events)
    {
        foreach (var group in FooterEvents(events).GroupBy(e => e.StreamId.Value!))
        {
            ApplyStreamSync(operations, group);
        }
    }

    /// <summary>
    /// Loads and incrementally updates the materialized document for each footer stream in an event batch.
    /// </summary>
    /// <param name="operations">The query/document session used to load and store projected documents.</param>
    /// <param name="events">The event batch to group by footer stream.</param>
    /// <param name="ct">A token that cancels projection loads.</param>
    /// <returns>A task that completes after all matching stream groups have been projected.</returns>
    /// <remarks>
    /// Events for other stream prefixes are ignored. Stream keys with the footer prefix but an invalid
    /// integer suffix cause an exception.
    /// </remarks>
    public async Task ApplyAsync(IDocumentOperations operations, IReadOnlyList<IEvent> events, CancellationToken ct)
    {
        foreach (var group in FooterEvents(events).GroupBy(e => e.StreamId.Value!))
        {
            await ApplyStreamAsync(operations, group, ct);
        }
    }

    private static IEnumerable<IEvent> FooterEvents(IEnumerable<IEvent> events)
        => events.Where(e => FooterStreams.IsFooterStream(e.StreamId.Value));

    private static void ApplyStreamSync(IDocumentOperations operations, IGrouping<string, IEvent> streamEvents)
    {
        var id = FooterStreams.ExtractFooterId(streamEvents.Key);
        FooterDocument? aggregate = null;

        foreach (var @event in streamEvents)
        {
            aggregate = ApplyEvent(aggregate, id, @event.Data);
        }

        if (aggregate is not null)
        {
            operations.Store(aggregate);
        }
    }

    private static async Task ApplyStreamAsync(IDocumentOperations operations, IGrouping<string, IEvent> streamEvents, CancellationToken ct)
    {
        var id = FooterStreams.ExtractFooterId(streamEvents.Key);
        var aggregate = await ((IQuerySession)operations).LoadAsync<FooterDocument>(id, ct);

        foreach (var @event in streamEvents)
        {
            aggregate = ApplyEvent(aggregate, id, @event.Data);
        }

        if (aggregate is not null)
        {
            operations.Store(aggregate);
        }
    }

    private static FooterDocument? ApplyEvent(FooterDocument? current, long id, object eventData)
        => eventData switch
        {
            FooterCreated e => FooterDocument.Create(id, e),
            FooterDraftSaved e => Apply(current, e),
            FooterPublished e => Apply(current, e),
            FooterArchived e => Apply(current, e),
            _ => current
        };

    private static FooterDocument? Apply(FooterDocument? current, FooterDraftSaved @event)
    {
        current?.Apply(@event);
        return current;
    }

    private static FooterDocument? Apply(FooterDocument? current, FooterPublished @event)
    {
        current?.Apply(@event);
        return current;
    }

    private static FooterDocument? Apply(FooterDocument? current, FooterArchived @event)
    {
        current?.Apply(@event);
        return current;
    }
}
