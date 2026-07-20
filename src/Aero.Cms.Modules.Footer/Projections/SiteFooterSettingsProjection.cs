using Aero.Cms.Modules.Footer.Domain;
using Aero.Cms.Modules.Footer.Events;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Footer.Projections;

/// <summary>
/// Projects site-default selection events into one <see cref="SiteFooterSettingsDocument"/> per site.
/// </summary>
public sealed class SiteFooterSettingsProjection : IProjection
{
    /// <inheritdoc />
    public Type[] EventTypes => [typeof(SiteDefaultFooterChanged)];

    /// <inheritdoc />
    public Task ApplyAsync(IProjectionContext context, CancellationToken ct)
        => ApplyAsync(context.Session, context.TypedEvents, ct);

    /// <summary>
    /// Applies complete event batches synchronously and stores one settings document per site stream.
    /// </summary>
    /// <param name="operations">The document session that receives projected settings documents.</param>
    /// <param name="events">The event batch to group by site-settings stream.</param>
    /// <remarks>
    /// This overload builds each document from the first matching event in the supplied batch.
    /// Stream keys with the settings prefix but an invalid integer suffix cause an exception.
    /// </remarks>
    public void Apply(IDocumentOperations operations, IReadOnlyList<IEvent> events)
    {
        foreach (var group in SiteSettingsEvents(events).GroupBy(e => e.StreamId.Value!))
        {
            ApplyStreamSync(operations, group);
        }
    }

    /// <summary>
    /// Loads and incrementally updates the settings document for each site stream in an event batch.
    /// </summary>
    /// <param name="operations">The query/document session used to load and store settings documents.</param>
    /// <param name="events">The event batch to group by site-settings stream.</param>
    /// <param name="ct">A token that cancels projection loads.</param>
    /// <returns>A task that completes after all matching stream groups have been projected.</returns>
    /// <remarks>
    /// Events for other stream prefixes are ignored. Stream keys with the settings prefix but an
    /// invalid integer suffix cause an exception.
    /// </remarks>
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
