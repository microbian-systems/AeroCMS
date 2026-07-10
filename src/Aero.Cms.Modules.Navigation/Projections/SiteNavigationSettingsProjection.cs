using Aero.Cms.Modules.Navigation.Domain;
using Aero.Cms.Modules.Navigation.Events;
using AeroDB;

namespace Aero.Cms.Modules.Navigation.Projections;

public sealed class SiteNavigationSettingsProjection : IProjection
{
    public void Apply(IDocumentOperations operations, IReadOnlyList<IEvent> events)
    {
        foreach (var group in SiteSettingsEvents(events).GroupBy(e => e.StreamId.Value!))
        {
            ApplyStreamSync(operations, group);
        }
    }

    public async Task ApplyAsync(IDocumentOperations operations, IReadOnlyList<IEvent> events, CancellationToken ct)
    {
        foreach (var group in SiteSettingsEvents(events).GroupBy(e => e.StreamId.Value!))
        {
            await ApplyStreamAsync(operations, group, ct);
        }
    }

    private static IEnumerable<IEvent> SiteSettingsEvents(IEnumerable<IEvent> events)
        => events.Where(e => NavMenuStreams.IsSiteSettingsStream(e.StreamId.Value));

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
