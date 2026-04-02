using Aero.Cms.Core.Blocks;

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
    Task<BlockBase?> GetByIdAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Saves a block to the persistent store.
    /// </summary>
    /// <param name="block">The block to save.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The saved block.</returns>
    Task<BlockBase> SaveAsync(BlockBase block, CancellationToken ct = default);
}
