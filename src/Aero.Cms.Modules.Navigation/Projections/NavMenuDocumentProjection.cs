using Aero.Cms.Modules.Navigation.Domain;
using Aero.Cms.Modules.Navigation.Events;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Navigation.Projections;

public sealed class NavMenuDocumentProjection : IProjection
{
    public void Apply(IDocumentOperations operations, IReadOnlyList<IEvent> events)
    {
        foreach (var group in MenuEvents(events).GroupBy(e => e.StreamId.Value!))
        {
            ApplyStreamSync(operations, group);
        }
    }

    public async Task ApplyAsync(IDocumentOperations operations, IReadOnlyList<IEvent> events, CancellationToken ct)
    {
        foreach (var group in MenuEvents(events).GroupBy(e => e.StreamId.Value!))
        {
            await ApplyStreamAsync(operations, group, ct);
        }
    }

    private static IEnumerable<IEvent> MenuEvents(IEnumerable<IEvent> events)
        => events.Where(e => NavMenuStreams.IsMenuStream(e.StreamId.Value));

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
