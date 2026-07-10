using Aero.Cms.Abstractions.Events;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Pages;

/// <summary>
/// One-time migration that bootstraps AeroDB event streams for existing pages.
/// Pages created before event sourcing was enabled don't have event streams.
/// This appends a synthetic PageCreated event to each page's stream so that
/// the snapshot projection knows about them and the event history is complete.
/// </summary>
public sealed class EventStreamBootstrapMigration
{
    private readonly IDocumentStore _store;
    private readonly ILogger<EventStreamBootstrapMigration> _logger;

    public EventStreamBootstrapMigration(IDocumentStore store, ILogger<EventStreamBootstrapMigration> logger)
    {
        _store = store;
        _logger = logger;
    }

    /// <summary>
    /// Returns true if any page lacks an event stream.
    /// </summary>
    public async Task<bool> IsMigrationNeededAsync(CancellationToken ct = default)
    {
        await using var session = await _store.LightweightSessionAsync();
        await using var query = await _store.QuerySessionAsync();

        var totalPages = await query.Query<PageDocument>().CountAsync(ct);
        if (totalPages == 0) return false;

        // Sample check: try to fetch a random page's event stream
        var samplePage = await query.Query<PageDocument>().FirstOrDefaultAsync(ct);
        if (samplePage is null) return false;

        var streamExists = await session.Events
            .QueryAllRawEvents()
            .Where(e => e.StreamId.Value == samplePage.Id.ToString()).AnyAsync(ct);

        return !streamExists;
    }

    /// <summary>
    /// Bootstraps event streams for all existing pages that don't have one.
    /// Each page gets a synthetic PageCreated event reflecting its current state.
    /// The inline snapshot projection is already in sync since PageDocument exists.
    /// </summary>
    public async Task BootstrapAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Starting event stream bootstrap migration...");

        await using var session = await _store.LightweightSessionAsync();
        await using var query = await _store.QuerySessionAsync();

        var pages = await query.Query<PageDocument>().ToListAsync(ct);
        var count = 0;
        var skipped = 0;

        foreach (var page in pages)
        {
            var streamId = page.Id.ToString();

            // Check if stream already exists
            var streamExists = await session.Events
                .QueryAllRawEvents()
                .Where(e => e.StreamId.Value == streamId).AnyAsync(ct);

            if (streamExists)
            {
                skipped++;
                continue;
            }

            // Append a synthetic PageCreated event reflecting current state
            session.Events.StartStream($"page-{page.Id}",
                new object[] { new PageCreated(
                    SiteId: page.SiteId,
                    Title: page.Title,
                    Slug: page.Slug,
                    ParentId: page.ParentId,
                    Order: page.Order,
                    PublicationState: page.PublicationState,
                    Kind: page.Kind) });

            count++;
        }

        if (count > 0)
        {
            await session.SaveChangesAsync(ct);
        }

        _logger.LogInformation(
            "Event stream bootstrap complete. Created {Created} streams, skipped {Skipped} existing.",
            count, skipped);
    }
}

