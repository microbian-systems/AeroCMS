using Aero.Cms.Abstractions.Events;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Pages;

/// <summary>
/// Custom IProjection for <see cref="PageDocument"/> that works with <c>long</c> (Snowflake)
/// stream identities.  AeroDB's built-in <c>SingleStreamProjection&lt;T, TId&gt;</c> only
/// supports <c>string</c> or <c>Guid</c> as TId, so we implement the low-level
/// <see cref="IProjection"/> interface directly.
/// </summary>
public sealed class PageDocumentProjection : IProjection
{
    // ── IProjection (sync) ──────────────────────────────────────────────

    public void Apply(
        IDocumentOperations operations,
        IReadOnlyList<IEvent> events)
    {
        foreach (var group in PageEvents(events).GroupBy(e => e.StreamId.Value!))
        {
            ApplyStreamSync(operations, group);
        }
    }

    // ── IProjection (async) ─────────────────────────────────────────────

    public async Task ApplyAsync(
        IDocumentOperations operations,
        IReadOnlyList<IEvent> events,
        CancellationToken ct)
    {
        foreach (var group in PageEvents(events).GroupBy(e => e.StreamId.Value!))
        {
            await ApplyStreamAsync(operations, group, ct);
        }
    }

    private static IEnumerable<IEvent> PageEvents(IEnumerable<IEvent> events)
        => events.Where(e => e.Data is PageCreated
            or PageContentUpdated
            or PageCompositionDraftSaved
            or PageCompositionPublished
            or PageMetadataUpdated
            or PagePublished
            or PageArchived
            or PageStateChanged
            or PageDeleted
            or PageRestored
            or PageMoved
            or PageVisibilityChanged);

    // ── Per-stream processing ──────────────────────────────────────────

    private static void ApplyStreamSync(
        IDocumentOperations operations,
        IGrouping<string, IEvent> streamEvents)
    {
        var id = ExtractLongId(streamEvents.Key);

        // Sync mode: we don't pre-load the existing aggregate.
        // The events in this batch should be sufficient to rebuild state.
        PageDocument? aggregate = null;

        foreach (var @event in streamEvents)
        {
            aggregate = ApplyEvent(aggregate, id, @event.Data);
        }

        if (aggregate is not null)
        {
            aggregate.Id = id;
            operations.Store(aggregate);
        }
    }

    private static async Task ApplyStreamAsync(
        IDocumentOperations operations,
        IGrouping<string, IEvent> streamEvents,
        CancellationToken ct)
    {
        var id = ExtractLongId(streamEvents.Key);

        // Async mode: load the existing aggregate so we apply new events
        // on top of the current persisted state.
        var aggregate = await ((IQuerySession)operations).LoadAsync<PageDocument>(id, ct);

        foreach (var @event in streamEvents)
        {
            aggregate = ApplyEvent(aggregate, id, @event.Data);
        }

        if (aggregate is not null)
        {
            aggregate.Id = id;
            operations.Store(aggregate);
        }
    }

    // ── Event → Aggregate evolution ────────────────────────────────────

    private static PageDocument? ApplyEvent(
        PageDocument? current,
        long id,
        object eventData)
    {
        switch (eventData)
        {
            case PageCreated e:
                var doc = PageDocument.Create(e);
                doc.Id = id;
                return doc;

            case PageContentUpdated e:
                current?.Apply(e);
                return current;

            case PageCompositionDraftSaved e:
                current?.Apply(e);
                return current;

            case PageCompositionPublished e:
                current?.Apply(e);
                return current;

            case PageMetadataUpdated e:
                current?.Apply(e);
                return current;

            case PagePublished e:
                current?.Apply(e);
                return current;

            case PageArchived:
                current?.Apply(new PageArchived());
                return current;

            case PageStateChanged e:
                current?.Apply(e);
                return current;

            case PageDeleted e:
                current?.Apply(e);
                return current;

            case PageRestored:
                current?.Apply(new PageRestored());
                return current;

            case PageMoved e:
                current?.Apply(e);
                return current;

            case PageVisibilityChanged e:
                current?.Apply(e);
                return current;

            default:
                return current;
        }
    }

    // ── Stream key → long ID ────────────────────────────────────────────

    private static long ExtractLongId(string streamKey)
    {
        if (long.TryParse(streamKey, out var id))
            return id;

        const string prefix = "page-";
        if (streamKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && long.TryParse(streamKey.AsSpan(prefix.Length), out id))
        {
            return id;
        }

        throw new InvalidOperationException(
            $"Cannot extract long ID from stream key: '{streamKey}'. " +
            $"Expected format: '{{id}}' or 'page-{{id}}'.");
    }
}
