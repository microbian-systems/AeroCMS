using Aero.Cms.Core.Entities;
using Aero.Core.Railway;
using AeroDB.Sable;
using Microsoft.Extensions.Logging;
using static Aero.Core.Railway.Prelude;

namespace Aero.Cms.Modules.Tenant;

/// <summary>
/// Defines unscoped document persistence operations for tenant records.
/// </summary>
public interface ITenantRepository
{
    /// <summary>
    /// Lists a page of tenants without applying an explicit ordering.
    /// </summary>
    /// <param name="page">The one-based page number; implementations clamp values below one.</param>
    /// <param name="num">The page size; the current implementation does not clamp invalid values.</param>
    /// <param name="ct">The token used for the query.</param>
    /// <returns>The returned tenant documents.</returns>
Task<IEnumerable<TenantModel>> GetAllAsync(int page = 1, int num = 10, CancellationToken ct = default);
    /// <summary>
    /// Finds a tenant by document identifier.
    /// </summary>
    /// <param name="id">The tenant identifier.</param>
    /// <param name="ct">The token used for the lookup.</param>
    /// <returns>An optional tenant.</returns>
Task<Option<TenantModel>> FindByIdAsync(long id, CancellationToken ct = default);
    /// <summary>
    /// Stores and commits a tenant document.
    /// </summary>
    /// <param name="entity">The tenant to store.</param>
    /// <param name="ct">The token used through commit.</param>
    /// <returns>The same tenant instance after commit.</returns>
Task<TenantModel> InsertAsync(TenantModel entity, CancellationToken ct = default);
    /// <summary>
    /// Stores and commits a replacement tenant document.
    /// </summary>
    /// <param name="entity">The tenant to store.</param>
    /// <param name="ct">The token used through commit.</param>
    /// <returns>The same tenant instance after commit.</returns>
Task<TenantModel> UpdateAsync(TenantModel entity, CancellationToken ct = default);
    /// <summary>
    /// Queues deletion by identifier and commits the session.
    /// </summary>
    /// <param name="id">The tenant identifier.</param>
    /// <param name="ct">The token used through commit and continuation scheduling.</param>
    /// <returns>Whether the save task completed successfully.</returns>
Task<bool> DeleteAsync(long id, CancellationToken ct = default);
}

/// <summary>
/// Provides data access and management operations for tenant entities using a document session.
/// </summary>
/// <param name="session">The document session used to interact with the underlying data store. Cannot be null.</param>
/// <param name="log">The injected logger; the current repository methods do not write log entries.</param>
public class TenantRepository(IDocumentSession session, ILogger<TenantRepository> log) : ITenantRepository
{
    /// <inheritdoc />
public async Task<IEnumerable<TenantModel>> GetAllAsync(int page = 1, int num = 10, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        var records = await session.Query<TenantModel>()
            .Skip((page - 1) * num)
            .Take(num)
            .ToListAsync(ct);
        return records?.AsEnumerable() ?? [];
    }

    /// <inheritdoc />
public async Task<Option<TenantModel>> FindByIdAsync(long id, CancellationToken ct = default)
    {
        var res = await session.LoadAsync<TenantModel>(id, ct);
        return res is not null ? Some(res) : None;
    }

    /// <inheritdoc />
public async Task<TenantModel> InsertAsync(TenantModel entity, CancellationToken ct = default)
    {
        session.Store(entity);
        await session.SaveChangesAsync(ct);
        return entity;
    }

    /// <inheritdoc />
public async Task<TenantModel> UpdateAsync(TenantModel entity, CancellationToken ct = default)
    {
        session.Store(entity);
        await session.SaveChangesAsync(ct);
        return entity;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Cancellation can prevent the continuation from running and propagate when awaited.
    /// Persistence exceptions are represented as <see langword="false"/> only when the
    /// continuation itself runs.
    /// </remarks>
public async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        session.Delete<TenantModel>(id);
        var result = await session.SaveChangesAsync(ct)
            .ContinueWith(t => t.IsCompletedSuccessfully, ct);
        return result;
    }
}