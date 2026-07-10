

namespace Aero.Cms.Abstractions.Blocks;

/// <summary>
/// Service for retrieving CMS blocks.
/// </summary>
public interface IBlockService
{
    /// <summary>
    /// Loads a block by its unique identifier.
    /// </summary>
    /// <param name="id">The block ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The block if found, otherwise null.</returns>
    /// <remarks>
    /// <b>N+1 WARNING:</b> This method performs a single-document database round-trip.
    /// When loading multiple blocks (e.g., for page rendering), prefer
    /// <see cref="GetByIdsAsync"/> to batch all IDs into a single query.
    ///
    /// Example of the problem — a page with 20 blocks calls this 20×:
    /// <code>
    /// foreach (var id in blockIds)
    ///     await blockService.GetByIdAsync(id, ct); // 20 separate DB queries
    /// </code>
    /// 
    /// Fix — single batch call with the same AeroDB LoadManyAsync under the hood:
    /// <code>
    /// var blocks = await blockService.GetByIdsAsync(blockIds, ct); // 1 DB query
    /// </code>
    /// </remarks>
    Task<BlockBase?> GetByIdAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Batch-loads blocks by their identifiers in a single database round-trip.
    /// </summary>
    /// <param name="ids">The block IDs to load.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A dictionary mapping each found block ID to its loaded block.
    /// IDs that were not found in the database are absent from the result
    /// (callers should fall back to null/placeholder logic per block).
    /// </returns>
    /// <remarks>
    /// Uses AeroDB's <c>LoadManyAsync</c> which issues a single <c>WHERE Id IN (...)</c>
    /// query to PostgreSQL. This eliminates N+1 round-trips during page rendering
    /// where all block placements on a page are resolved together.
    ///
    /// Prefer this over multiple <see cref="GetByIdAsync"/> calls when loading
    /// two or more blocks in the same request scope.
    /// </remarks>
    Task<IReadOnlyDictionary<long, BlockBase>> GetByIdsAsync(
        IEnumerable<long> ids, CancellationToken ct = default);

    /// <summary>
    /// Saves a block to the persistent store.
    /// </summary>
    /// <param name="block">The block to save.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The saved block.</returns>
    Task<BlockBase> SaveAsync(BlockBase block, CancellationToken ct = default);
}
