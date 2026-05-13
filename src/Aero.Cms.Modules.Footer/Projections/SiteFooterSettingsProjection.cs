using Aero.Cms.Modules.Footer.Domain;
using Aero.Cms.Modules.Footer.Events;
using JasperFx.Events;
using Marten.Events;
using Marten.Events.Projections;

namespace Aero.Cms.Modules.Footer.Projections;

public sealed class SiteFooterSettingsProjection : IProjection
{
    public void Apply(IDocumentOperations operations, IReadOnlyList<IEvent> events)
    {
        foreach (var group in SiteSettingsEvents(events).GroupBy(e => e.StreamKey!))
        {
            ApplyStreamSync(operations, group);
        }
    }

    public async Task ApplyAsync(IDocumentOperations operations, IReadOnlyList<IEvent> events, CancellationToken ct)
    {
        foreach (var group in SiteSettingsEvents(events).GroupBy(e => e.StreamKey!))
        {
            await ApplyStreamAsync(operations, group, ct);
        }
    }

    private static IEnumerable<IEvent> SiteSettingsEvents(IEnumerable<IEvent> events)
        => events.Where(e => FooterStreams.IsSiteSettingsStream(e.StreamKey));

    private static void ApplyStreamSync(IDocumentOperations operations, IGrouping<string, IEvent> streamEvents)
    {
        var siteId = FooterStreams.ExtractSiteId(streamEvents.Key);
        SiteFooterSettingsDocument? aggregate = null;

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
        var siteId = FooterStreams.ExtractSiteId(streamEvents.Key);
        var aggregate = await operations.LoadAsync<SiteFooterSettingsDocument>(siteId, ct);

        foreach (var @event in streamEvents)
        {
            aggregate = ApplyEvent(aggregate, siteId, @event.Data);
        }

        if (aggregate is not null)
        {
            operations.Store(aggregate);
        }
    }

    private static SiteFooterSettingsDocument? ApplyEvent(SiteFooterSettingsDocument? current, long siteId, object eventData)
    {
        if (eventData is not SiteDefaultFooterChanged e)
        {
            return current;
        }

        if (current is null)
        {
            return SiteFooterSettingsDocument.Create(siteId, e);
        }

        current.Apply(e);
        return current;
    }
}
