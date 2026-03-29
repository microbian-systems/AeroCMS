using Aero.Cms.Core.Blocks;
using Marten;

namespace Aero.Cms.Web.Core.Blocks;

/// <summary>
/// Marten-backed implementation of <see cref="IBlockService"/>.
/// </summary>
public sealed class MartenBlockService : IBlockService
{
    private readonly IDocumentSession _session;

    public MartenBlockService(IDocumentSession session)
    {
        _session = session;
    }

    public Task<BlockBase?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return _session.LoadAsync<BlockBase>(id, ct);
    }

    public async Task<BlockBase> SaveAsync(BlockBase block, CancellationToken ct = default)
    {
        _session.Store(block);
        await _session.SaveChangesAsync(ct);
        return block;
    }
}
