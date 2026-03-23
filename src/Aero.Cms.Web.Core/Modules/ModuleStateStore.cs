using Marten;

namespace Aero.Cms.Web.Core.Modules;

/// <summary>
/// Marten-backed implementation of <see cref="IModuleStateStore"/>.
/// </summary>
public sealed class ModuleStateStore(IDocumentSession session) : IModuleStateStore
{
    /// <inheritdoc/>
    public Task<IReadOnlyList<ModuleStateDocument>> GetAllAsync(CancellationToken ct = default)
    {
        return session.Query<ModuleStateDocument>().ToListAsync(ct);
    }

    /// <inheritdoc/>
    public Task SaveAllAsync(IEnumerable<ModuleStateDocument> modules, CancellationToken ct = default)
    {
        foreach (var module in modules)
        {
            session.Store(module);
        }
        return session.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public Task<ModuleStateDocument?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        var id = $"{ModuleStateDocument.ModuleIdPrefix}{name}";
        return session.LoadAsync<ModuleStateDocument>(id, ct);
    }
}
