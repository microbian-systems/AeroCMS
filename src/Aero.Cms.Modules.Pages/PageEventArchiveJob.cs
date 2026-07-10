using TickerQ.Utilities.Base;

namespace Aero.Cms.Modules.Pages;

/// <summary>
/// Daily TickerQ job that archives old page events.
/// Events older than the retention period (default: 90 days) are archived.
/// </summary>
public sealed class PageEventArchiveJob(
    IDocumentStore store,
    ILogger<PageEventArchiveJob> log)
{
    private const int DefaultRetentionDays = 90;

        /// <summary>
    /// ArchiveOldEvents method.
    /// </summary>
[TickerFunction("pages.archive-events")]
    public async Task ArchiveOldEvents(
        TickerFunctionContext context,
        CancellationToken cancellationToken)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-DefaultRetentionDays);

        await using var session = await store.LightweightSessionAsync();

        var oldEvents = await session.Events
            .QueryAllRawEvents()
            .Where(e => e.Timestamp < cutoff)
            .ToListAsync(cancellationToken);

        if (oldEvents.Count == 0)
        {
            log.LogInformation("No page events older than {Days} days to archive.", DefaultRetentionDays);
            return;
        }

        log.LogInformation("Archiving {Count} page events older than {Days} days",
            oldEvents.Count, DefaultRetentionDays);

        foreach (var e in oldEvents)
        {
            session.Events.ArchiveStream(e.StreamId.Value);
        }

        await session.SaveChangesAsync(cancellationToken);

        log.LogInformation("Page event archive complete. {Count} events archived.", oldEvents.Count);
    }
}
