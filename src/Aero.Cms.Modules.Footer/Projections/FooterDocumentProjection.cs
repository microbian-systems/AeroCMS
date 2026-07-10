using Aero.Cms.Modules.Footer.Domain;
using Aero.Cms.Modules.Footer.Events;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Footer.Projections;

public sealed class FooterDocumentProjection : IProjection
{
    public void Apply(IDocumentOperations operations, IReadOnlyList<IEvent> events)
    {
        foreach (var group in FooterEvents(events).GroupBy(e => e.StreamId.Value!))
        {
            ApplyStreamSync(operations, group);
        }
    }

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
