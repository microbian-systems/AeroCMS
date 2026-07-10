using Aero.Modular;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Modules.Services;

/// <summary>
/// AeroDB-backed implementation of <see cref="IModuleStateStore"/>.
/// </summary>
public sealed class ModuleStateStore : IModuleStateStore
{
    private readonly IDocumentSession _session;

        /// <summary>
    /// Initializes a new instance of the <see cref="ModuleStateStore"/> class.
    /// </summary>
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
    public Task<ModuleDocument?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        var module = _session.Query<ModuleDocument>().FirstOrDefaultAsync(m => m.Name == name);
        return module;
    }
}
