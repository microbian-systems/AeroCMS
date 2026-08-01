using Aero.Cms.Abstractions.Blocks;
using Marten;

namespace Aero.Cms.Core.Blocks;

/// <summary>
/// Marten-backed implementation of <see cref="IBlockService"/>.
/// </summary>
public sealed class MartenBlockService : IBlockService
{
    private readonly IDocumentSession _session;

        /// <summary>
    /// Initializes a new instance of the <see cref="MartenBlockService"/> class.
    /// </summary>
public MartenBlockService(IDocumentSession session)
    {
        _session = session;
    }

    /// <summary>
    /// Single-block lookup. Prefer <see cref="GetByIdsAsync"/> for batch loads.
    /// </summary>
    public Task<BlockBase?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return _session.LoadAsync<BlockBase>(id, ct);
    }

    /// <summary>
    /// Batch-loads blocks using Marten's LINQ query with IsOneOf, which issues a single
    /// <c>WHERE d.id = ANY(@p0)</c> PostgreSQL query instead of N individual lookups.
    /// This is the recommended method for page rendering pipelines where all
    /// block placements are resolved together.
    /// 
    /// Uses the same pattern already proven in PagePublishingWorkflowService
    /// (<c>Query&lt;BlockBase&gt;().Where(b => b.Id.IsOneOf(ids))</c>).
    /// </summary>
    public async Task<IReadOnlyDictionary<long, BlockBase>> GetByIdsAsync(
        IEnumerable<long> ids, CancellationToken ct = default)
    {
        var idsList = ids as IReadOnlyList<long> ?? ids.ToList();
        if (idsList.Count == 0)
            return new Dictionary<long, BlockBase>();

        // Single Marten query: WHERE d.id = ANY(@p0)
        // Uses the same IsOneOf pattern from PagePublishingWorkflowService
        // line 138-140, verified to produce correct SQL in Marten 8.37.0.
        var blocks = await _session.Query<BlockBase>()
            .Where(b => b.Id.IsOneOf(idsList.ToArray()))
            .ToListAsync(ct);

        var result = new Dictionary<long, BlockBase>(blocks.Count);
        foreach (var block in blocks)
        {
            if (block is not null)
                result[block.Id] = block;
        }

        return result;
    }

        /// <summary>
    /// SaveAsync method.
    /// </summary>
public async Task<BlockBase> SaveAsync(BlockBase block, CancellationToken ct = default)
    {
        _session.Store(block);
        await _session.SaveChangesAsync(ct);
        return block;
    }
}
