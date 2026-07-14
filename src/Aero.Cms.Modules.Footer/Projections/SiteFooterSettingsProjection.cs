using Aero.Cms.Modules.Footer.Domain;
using Aero.Cms.Modules.Footer.Events;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Footer.Projections;

/// <summary>
/// Represents a class for SiteFooterSettingsProjection.
/// </summary>
public sealed class SiteFooterSettingsProjection : IProjection
{
    public Type[] EventTypes => [typeof(SiteDefaultFooterChanged)];

    public Task ApplyAsync(IProjectionContext context, CancellationToken ct)
        => ApplyAsync(context.Session, context.TypedEvents, ct);

        /// <summary>
    /// Apply method.
    /// </summary>
public void Apply(IDocumentOperations operations, IReadOnlyList<IEvent> events)
    {
        foreach (var group in SiteSettingsEvents(events).GroupBy(e => e.StreamId.Value!))
        {
            ApplyStreamSync(operations, group);
        }
    }

        /// <summary>
    /// ApplyAsync method.
    /// </summary>
public async Task ApplyAsync(IDocumentOperations operations, IReadOnlyList<IEvent> events, CancellationToken ct)
    {
        foreach (var group in SiteSettingsEvents(events).GroupBy(e => e.StreamId.Value!))
        {
            await ApplyStreamAsync(operations, group, ct);
        }
    }

    private static IEnumerable<IEvent> SiteSettingsEvents(IEnumerable<IEvent> events)
        => events.Where(e => FooterStreams.IsSiteSettingsStream(e.StreamId.Value));

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
        var aggregate = await ((IQuerySession)operations).LoadAsync<SiteFooterSettingsDocument>(siteId, ct);

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
