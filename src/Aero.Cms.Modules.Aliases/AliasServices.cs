using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Repositories;
using Aero.Cms.Modules.Aliases.Events;
using AeroDB.Sable;
using Wolverine;

namespace Aero.Cms.Modules.Aliases;

/// <summary>
/// Defines an interface for IAliasService.
/// </summary>
public interface IAliasService
{
        /// <summary>
    /// GetBySiteIdAsync method.
    /// </summary>
Task<IList<AliasDocument>> GetBySiteIdAsync(long siteId, CancellationToken ct = default);
        /// <summary>
    /// GetByOldPathAsync method.
    /// </summary>
Task<AliasDocument?> GetByOldPathAsync(string oldPath, CancellationToken ct = default);
        /// <summary>
    /// CreateAsync method.
    /// </summary>
Task<AliasDocument> CreateAsync(AliasDocument document, CancellationToken ct = default);
        /// <summary>
    /// Update method.
    /// </summary>
AliasDocument Update(AliasDocument document);
        /// <summary>
    /// Delete method.
    /// </summary>
void Delete(AliasDocument document);
        /// <summary>
    /// DeleteAsync method.
    /// </summary>
Task DeleteAsync(long id, CancellationToken ct = default);
}

/// <summary>
/// Service layer for alias CRUD operations. Publishes Wolverine events
/// after each mutation to trigger cache invalidation via
/// <see cref="Handlers.AliasCacheInvalidationHandler"/>.
/// </summary>
public class AliasService : IAliasService
{
    private readonly IAliasRepository _repo;
    private readonly IDocumentSession _session;
    private readonly IMessageBus _bus;

        /// <summary>
    /// Initializes a new instance of the <see cref="AliasService"/> class.
    /// </summary>
public AliasService(IAliasRepository repo, IDocumentSession session, IMessageBus bus)
    {
        _repo = repo;
        _session = session;
        _bus = bus;
    }

        /// <summary>
    /// GetBySiteIdAsync method.
    /// </summary>
public Task<IList<AliasDocument>> GetBySiteIdAsync(long siteId, CancellationToken ct = default)
        => _repo.GetBySiteIdAsync(siteId, ct);

        /// <summary>
    /// GetByOldPathAsync method.
    /// </summary>
public Task<AliasDocument?> GetByOldPathAsync(string oldPath, CancellationToken ct = default)
        => _repo.GetByOldPathAsync(oldPath, ct);

        /// <summary>
    /// CreateAsync method.
    /// </summary>
public async Task<AliasDocument> CreateAsync(AliasDocument document, CancellationToken ct = default)
    {
        await _repo.AddAsync(document, ct);
        await _session.SaveChangesAsync(ct);
        await _bus.PublishAsync(new AliasCreated(document));
        return document;
    }

        /// <summary>
    /// Update method.
    /// </summary>
public AliasDocument Update(AliasDocument document)
    {
        _repo.Update(document);
        _session.SaveChangesAsync().GetAwaiter().GetResult();
        _bus.PublishAsync(new AliasUpdated(document));
        return document;
    }

        /// <summary>
    /// Delete method.
    /// </summary>
public void Delete(AliasDocument document)
    {
        _repo.Delete(document);
        _session.SaveChangesAsync().GetAwaiter().GetResult();
        _bus.PublishAsync(new AliasDeleted(document));
    }

        /// <summary>
    /// DeleteAsync method.
    /// </summary>
public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var existing = await _repo.GetByIdAsync(id, ct);
        if (existing is not null)
        {
            _repo.Delete(existing);
            await _session.SaveChangesAsync(ct);
            await _bus.PublishAsync(new AliasDeleted(existing));
        }
    }
}
