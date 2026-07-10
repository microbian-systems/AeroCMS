using Aero.Cms.Core.Entities;
using Aero.Core.Railway;
using AeroDB.Sable;
using Microsoft.Extensions.Logging;
using static Aero.Core.Railway.Prelude;

namespace Aero.Cms.Modules.Tenant;

public interface ITenantRepository
{
    Task<IEnumerable<TenantModel>> GetAllAsync(int page = 1, int num = 10, CancellationToken ct = default);
    Task<Option<TenantModel>> FindByIdAsync(long id, CancellationToken ct = default);
    Task<TenantModel> InsertAsync(TenantModel entity, CancellationToken ct = default);
    Task<TenantModel> UpdateAsync(TenantModel entity, CancellationToken ct = default);
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);
}

/// <summary>
/// Provides data access and management operations for tenant entities using a document session.
/// </summary>
/// <param name="session">The document session used to interact with the underlying data store. Cannot be null.</param>
/// <param name="log">The logger instance used for logging repository operations. Cannot be null.</param>
public class TenantRepository(IDocumentSession session, ILogger<TenantRepository> log) : ITenantRepository
{
    public async Task<IEnumerable<TenantModel>> GetAllAsync(int page = 1, int num = 10, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        var records = await session.Query<TenantModel>()
            .Skip((page - 1) * num)
            .Take(num)
            .ToListAsync(ct);
        return records?.AsEnumerable() ?? [];
    }

    public async Task<Option<TenantModel>> FindByIdAsync(long id, CancellationToken ct = default)
    {
        var res = await session.LoadAsync<TenantModel>(id, ct);
        return res is not null ? Some(res) : None;
    }

    public async Task<TenantModel> InsertAsync(TenantModel entity, CancellationToken ct = default)
    {
        session.Store(entity);
        await session.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<TenantModel> UpdateAsync(TenantModel entity, CancellationToken ct = default)
    {
        session.Store(entity);
        await session.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        session.Delete<TenantModel>(id);
        var result = await session.SaveChangesAsync(ct)
            .ContinueWith(t => t.IsCompletedSuccessfully, ct);
        return result;
    }
}