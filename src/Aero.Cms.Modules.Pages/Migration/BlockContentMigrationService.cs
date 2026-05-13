using System.Text.Json;
using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Core.Entities;
using Marten;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Pages.Migration;

/// <summary>
/// Default implementation of <see cref="IBlockContentMigrationService"/>.
/// Runs inside the caller's ambient <see cref="IDocumentSession"/> (typically
/// managed by a Wolverine handler for transactional safety).
/// </summary>
internal sealed class BlockContentMigrationService : IBlockContentMigrationService
{
    private readonly IDocumentSession _session;
    private readonly ILegacyBlockMapper _mapper;
    private readonly ILogger<BlockContentMigrationService> _logger;

    /// <summary>
    /// First Neo block schema. Increment on each coordinated schema migration.
    /// </summary>
    public const int SchemaVersion = 1;

    public int CurrentSchemaVersion => SchemaVersion;

    public BlockContentMigrationService(
        IDocumentSession session,
        ILegacyBlockMapper mapper,
        ILogger<BlockContentMigrationService> logger)
    {
        _session = session;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<BlockMigrationResult> MigratePageAsync(long pageId, CancellationToken ct = default)
    {
        var affectedIds = new List<long>();
        var errors = new List<string>();
        int migrated = 0, skipped = 0;

        try
        {
            var page = await _session.LoadAsync<PageDocument>(pageId, ct);
            if (page is null)
            {
                errors.Add($"Page {pageId} not found");
                return new BlockMigrationResult(0, 0, 1, [pageId], errors);
            }

            affectedIds.Add(pageId);

            // Idempotency: skip pages already at or above current schema
            if (page.BlockSchemaVersion >= SchemaVersion)
            {
                skipped++;
                _logger.LogDebug("Page {PageId} already at block schema {Version}", pageId, page.BlockSchemaVersion);
                return new BlockMigrationResult(0, 1, 0, affectedIds, Enumerable.Empty<string>().ToList());
            }

            // Load editor state — must exist (created by Step 3 PageDocumentMigration)
            var editorState = await _session.LoadAsync<PageEditorState>(pageId, ct);
            if (editorState is null)
            {
                _logger.LogWarning("Page {PageId} has no PageEditorState — run Step 3 document migration first", pageId);
                return new BlockMigrationResult(0, 0, 0, affectedIds,
                    new List<string> { $"No PageEditorState for page {pageId}. Run PageDocumentMigration (Step 3) first." });
            }

            if (editorState.Blocks.Count == 0)
            {
                // No blocks to migrate — just bump the schema version
                page.BlockSchemaVersion = SchemaVersion;
                _session.Store(page);
                await _session.SaveChangesAsync(ct);

                skipped++;
                _logger.LogDebug("Page {PageId} has no editor blocks; schema version bumped", pageId);
                return new BlockMigrationResult(0, 1, 0, affectedIds, Enumerable.Empty<string>().ToList());
            }

            // ── Migrate each placement ──────────────────────────
            int mappedCount = 0;
            var newBlockIds = new HashSet<long>();

            foreach (var placement in editorState.Blocks)
            {
                if (placement.BlockId is not { } blockId)
                    continue;

                var oldBlock = await _session.LoadAsync<BlockBase>(blockId, ct);
                if (oldBlock is null)
                    continue;

                var nodes = _mapper.MapFromBlock(oldBlock);
                if (nodes.Count == 0)
                    continue;

                // Create a NeoCompositionBlock wrapping the mapped nodes
                var neoBlock = new NeoCompositionBlock
                {
                    Nodes = nodes,
                };

                _session.Store(neoBlock);
                newBlockIds.Add(neoBlock.Id);

                // Update the placement to point to the new block
                placement.BlockId = neoBlock.Id;

                // Register the new block in BlockIdMap (keyed by client ID)
                if (!string.IsNullOrEmpty(placement.ClientId))
                {
                    editorState.BlockIdMap[placement.ClientId] = neoBlock.Id;
                }

                mappedCount++;
            }

            // ── Persist ─────────────────────────────────────────
            page.BlockSchemaVersion = SchemaVersion;
            _session.Store(editorState);
            _session.Store(page);
            await _session.SaveChangesAsync(ct);

            migrated++;
            _logger.LogInformation(
                "Migrated page {PageId}: {MappedCount} blocks → {NewBlockCount} NeoCompositionBlocks",
                pageId, mappedCount, newBlockIds.Count);

            return new BlockMigrationResult(migrated, skipped, 0, affectedIds, Enumerable.Empty<string>().ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Migration failed for page {PageId}", pageId);
            errors.Add($"Page {pageId}: {ex.Message}");
            return new BlockMigrationResult(0, 0, 1, affectedIds, errors);
        }
    }

    public async Task<BlockMigrationResult> MigrateSiteAsync(long siteId, CancellationToken ct = default)
    {
        var totalMigrated = 0;
        var totalSkipped = 0;
        var totalFailed = 0;
        var allAffected = new List<long>();
        var allErrors = new List<string>();

        // Find all non-deleted pages for the site
        var pages = await _session.Query<PageDocument>()
            .Where(p => p.SiteId == siteId && !p.Deleted)
            .Select(p => p.Id)
            .ToListAsync(ct);

        _logger.LogInformation("Starting block migration for site {SiteId} ({Count} pages)", siteId, pages.Count);

        foreach (var pageId in pages)
        {
            var result = await MigratePageAsync(pageId, ct);
            totalMigrated += result.Migrated;
            totalSkipped += result.Skipped;
            totalFailed += result.Failed;
            allAffected.AddRange(result.AffectedPageIds);
            allErrors.AddRange(result.Errors);
        }

        _logger.LogInformation(
            "Site {SiteId} block migration complete: {Migrated} migrated, {Skipped} skipped, {Failed} failed",
            siteId, totalMigrated, totalSkipped, totalFailed);

        return new BlockMigrationResult(totalMigrated, totalSkipped, totalFailed, allAffected, allErrors);
    }
}
