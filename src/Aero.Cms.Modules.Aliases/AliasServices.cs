using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Repositories;
using Aero.Cms.Modules.Aliases.Events;
using AeroDB.Sable;
using Wolverine;

namespace Aero.Cms.Modules.Aliases;

public interface IAliasService
{
    Task<IList<AliasDocument>> GetBySiteIdAsync(long siteId, CancellationToken ct = default);
    Task<AliasDocument?> GetByOldPathAsync(string oldPath, CancellationToken ct = default);
    Task<AliasDocument> CreateAsync(AliasDocument document, CancellationToken ct = default);
    AliasDocument Update(AliasDocument document);
    void Delete(AliasDocument document);
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

    public AliasService(IAliasRepository repo, IDocumentSession session, IMessageBus bus)
    {
        _repo = repo;
        _session = session;
        _bus = bus;
    }

    public Task<IList<AliasDocument>> GetBySiteIdAsync(long siteId, CancellationToken ct = default)
        => _repo.GetBySiteIdAsync(siteId, ct);

    public Task<AliasDocument?> GetByOldPathAsync(string oldPath, CancellationToken ct = default)
        => _repo.GetByOldPathAsync(oldPath, ct);

    public async Task<AliasDocument> CreateAsync(AliasDocument document, CancellationToken ct = default)
    {
        await _repo.AddAsync(document, ct);
        await _session.SaveChangesAsync(ct);
        await _bus.PublishAsync(new AliasCreated(document));
        return document;
    }

    public AliasDocument Update(AliasDocument document)
    {
        _repo.Update(document);
        _session.SaveChangesAsync().GetAwaiter().GetResult();
        _bus.PublishAsync(new AliasUpdated(document));
        return document;
    }

    public void Delete(AliasDocument document)
    {
        _repo.Delete(document);
        _session.SaveChangesAsync().GetAwaiter().GetResult();
        _bus.PublishAsync(new AliasDeleted(document));
    }

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
