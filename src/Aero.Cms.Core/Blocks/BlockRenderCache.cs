using Aero.Cms.Abstractions.Blocks;

namespace Aero.Cms.Core.Blocks;

/// <summary>
/// Per-request cache that eliminates N+1 database round-trips during page rendering.
///
/// <h2>N+1 Problem</h2>
/// A public page contains multiple <c>BlockPlacement</c> entries, each rendered by
/// a separate <c>BlockPlacementRenderer</c> Blazor component. Without this cache,
/// every renderer independently calls <c>IBlockService.GetByIdAsync()</c>, producing
/// one Marten <c>LoadAsync&lt;BlockBase&gt;(id)</c> per block. A page with 40 blocks
/// makes 40 separate PostgreSQL queries.
///
/// <h2>Solution</h2>
/// <b>Phase 1 — Preload (in DynamicPageModel.OnGetAsync)</b>:
/// The page model collects all <c>BlockId</c> values from every <c>LayoutRegion</c>,
/// <c>LayoutColumn</c>, and <c>BlockPlacement</c> on the page, then calls
/// <c>IBlockService.GetByIdsAsync()</c> to load all blocks in a single
/// <c>WHERE Id = ANY(@ids)</c> query. Results are stored in this cache.
///
/// <b>Phase 2 — Lookup (in BlockPlacementRenderer)</b>:
/// Each renderer calls the synchronous <c>GetBlock(long)</c> method, which performs
/// an O(1) dictionary lookup instead of a database query.
///
/// <h2>Why scoped?</h2>
/// Registered as <c>AddScoped</c> so the same instance is shared between the
/// <c>DynamicPageModel</c> and all <c>&lt;component&gt;</c> tag helpers within
/// a single HTTP request.
/// </summary>
public class BlockRenderCache
{
    /// <summary>
    /// Lazily-built index: maps block ID to loaded block document.
    /// </summary>
    private Dictionary<long, BlockBase>? _index;

    /// <summary>
    /// Whether the preload phase has been executed for the current request.
    /// If false when a renderer queries, the renderer will log a warning —
    /// the page model forgot to call PreloadAsync, causing a fallback to
    /// per-block queries (degraded performance but not a crash).
    /// </summary>
    public bool IsLoaded => _index is not null;

    /// <summary>
    /// Bulk-loads all block IDs in a single batch query and populates the
    /// internal lookup index. Must be called exactly once per request,
    /// typically in <c>DynamicPageModel.OnGetAsync</c> before the view renders.
    /// </summary>
    /// <param name="ids">All distinct block IDs found on the page.</param>
    /// <param name="blockService">The block service for batch loading.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task PreloadAsync(
        IEnumerable<long> ids,
        IBlockService blockService,
        CancellationToken ct = default)
    {
        if (_index is not null)
            return; // idempotent — avoid double-load if called more than once

        var idsList = ids as IReadOnlyList<long> ?? ids.ToList();
        if (idsList.Count == 0)
        {
            _index = new Dictionary<long, BlockBase>();
            return;
        }

        _index = new Dictionary<long, BlockBase>(
            await blockService.GetByIdsAsync(idsList, ct));
    }

    /// <summary>
    /// Retrieves a preloaded block by ID. O(1) dictionary lookup — no database call.
    /// Returns null if the block was not found during preload or if preload has not
    /// been called yet.
    /// </summary>
    /// <param name="id">The block ID to look up.</param>
    /// <returns>The cached block, or null if not found / not yet loaded.</returns>
    public BlockBase? GetBlock(long id)
    {
        if (_index is null)
            return null;

        _index.TryGetValue(id, out var block);
        return block;
    }
}
