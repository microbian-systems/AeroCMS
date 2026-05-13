using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Core.Entities;
using Marten;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Pages;

/// <summary>
/// One-time migration that moves editor block placement state from
/// <see cref="PageDocument"/> to <see cref="PageEditorState"/>.
/// Idempotent — safe to run multiple times. Should be run before
/// the new NeoUI PageEditor is enabled.
/// </summary>
public sealed class PageDocumentMigration
{
    private readonly IDocumentSession _session;
    private readonly ILogger<PageDocumentMigration> _logger;

    public PageDocumentMigration(
        IDocumentSession session,
        ILogger<PageDocumentMigration> logger)
    {
        _session = session;
        _logger = logger;
    }

    /// <summary>
    /// Migrates editor state from <see cref="PageDocument"/> to
    /// <see cref="PageEditorState"/> for all pages in the current site.
    /// Preserves existing published <see cref="PageDocument.LayoutRegions"/>.
    /// </summary>
    /// <param name="siteId">The site to migrate pages for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A tuple of (pages with editor state, pages without, errors).
    /// </returns>
    public async Task<MigrationResult> MigrateAsync(
        long siteId,
        CancellationToken ct = default)
    {
        var migrated = 0;
        var emptyStateCreated = 0;
        var skipped = 0;
        var errors = new List<string>();
        var affectedPageIds = new List<long>();

        try
        {
            // Load all non-deleted pages for the site
            var pages = await _session.Query<PageDocument>()
                .Where(x => x.SiteId == siteId && !x.Deleted)
                .ToListAsync(ct);

            _logger.LogInformation(
                "Starting PageDocument → PageEditorState migration for site {SiteId} ({PageCount} pages)",
                siteId, pages.Count);

            foreach (var page in pages)
            {
                try
                {
                    affectedPageIds.Add(page.Id);

                    // Check if PageEditorState already exists (idempotency)
                    var existing = await _session.LoadAsync<PageEditorState>(page.Id, ct);

                    if (page.Blocks.Count > 0 && existing is null)
                    {
                        // Migrate pages with editor blocks
                        var state = CreateEditorStateFromPage(page);
                        _session.Store(state);
                        migrated++;
                    }
                    else if (page.Blocks.Count == 0 && existing is null)
                    {
                        // LayoutRegions-only pages: create empty editor state
                        var state = CreateEmptyEditorState(page);
                        _session.Store(state);
                        emptyStateCreated++;
                    }
                    else
                    {
                        skipped++;
                    }
                }
                catch (Exception ex)
                {
                    var msg = $"Migration failed for page {page.Id}: {ex.Message}";
                    _logger.LogWarning(ex, msg);
                    errors.Add(msg);
                }
            }

            await _session.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Migration complete: {Migrated} editor states, {Empty} empty states, {Skipped} skipped, {Errors} errors",
                migrated, emptyStateCreated, skipped, errors.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Batch migration failed for site {SiteId}", siteId);
            errors.Add($"Batch failure: {ex.Message}");
        }

        return new MigrationResult(migrated, emptyStateCreated, skipped, errors, affectedPageIds);
    }

    private static PageEditorState CreateEditorStateFromPage(PageDocument page)
    {
        var placements = new List<EditorBlockPlacement>(page.Blocks.Count);
        for (var i = 0; i < page.Blocks.Count; i++)
        {
            var eb = page.Blocks[i];
            long? blockId = null;
            if (!string.IsNullOrEmpty(eb.EditorId) && page.BlockIdMap.TryGetValue(eb.EditorId, out var id))
            {
                blockId = id;
            }

            placements.Add(new EditorBlockPlacement
            {
                ClientId = eb.EditorId ?? string.Empty,
                BlockId = blockId,
                Region = "main",
                Order = i
            });
        }

        return new PageEditorState
        {
            Id = page.Id,
            SiteId = page.SiteId,
            DraftVersion = page.PublishedVersion > 0
                ? page.PublishedVersion + 1
                : 1,
            Blocks = placements,
            BlockIdMap = new Dictionary<string, long>(page.BlockIdMap),
            LastModified = page.ModifiedOn ?? DateTimeOffset.UtcNow
        };
    }

    private static PageEditorState CreateEmptyEditorState(PageDocument page)
    {
        return new PageEditorState
        {
            Id = page.Id,
            SiteId = page.SiteId,
            DraftVersion = 0,
            Blocks = [],
            BlockIdMap = [],
            LastModified = page.ModifiedOn ?? DateTimeOffset.UtcNow
        };
    }
}

/// <summary>
/// Result of a <see cref="PageDocumentMigration"/> run.
/// </summary>
/// <param name="Migrated">Pages with editor blocks that received a new PageEditorState.</param>
/// <param name="EmptyStatesCreated">LayoutRegions-only pages that received an empty PageEditorState.</param>
/// <param name="Skipped">Pages already migrated (PageEditorState already exists).</param>
/// <param name="Errors">Error messages collected during migration.</param>
/// <param name="AffectedPageIds">All page IDs processed during this run (for rollback tracking).</param>
public sealed record MigrationResult(
    int Migrated,
    int EmptyStatesCreated,
    int Skipped,
    IReadOnlyList<string> Errors,
    IReadOnlyList<long> AffectedPageIds);
