using Aero.Modular;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Modules.Services;

/// <summary>
/// AeroDB-backed implementation of <see cref="IModuleStateStore"/>.
/// </summary>
/// <remarks>
/// The injected session is reused by the scoped store. Save operations stage all supplied documents
/// and commit them together through that session.
/// </remarks>
public sealed class ModuleStateStore : IModuleStateStore
{
    private readonly IDocumentSession _session;

        /// <summary>
    /// Initializes the store over an existing document session.
    /// </summary>
    /// <param name="session">The scoped session used by all store operations.</param>
public ModuleStateStore(IDocumentSession session)
    {
        _session = session;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ModuleDocument>> GetAllAsync(CancellationToken ct = default)
    {
        return await _session.Query<ModuleDocument>().ToListAsync(ct);
    }

    /// <inheritdoc/>
    public Task SaveAllAsync(IEnumerable<ModuleDocument> modules, CancellationToken ct = default)
    {
        foreach (var module in modules)
        {
            _session.Store(module);
        }
        return _session.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The current implementation does not pass <paramref name="ct"/> to the provider query.
    /// Cancellation therefore does not stop this lookup through this contract.
    /// </remarks>
    public Task<ModuleDocument?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        var module = _session.Query<ModuleDocument>().FirstOrDefaultAsync(m => m.Name == name);
        return module;
    }
}
