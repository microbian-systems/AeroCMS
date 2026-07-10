using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Queries;
using Aero.Core.Railway;
using AeroDB.Sable;
using Microsoft.Extensions.Logging;
using static Aero.Core.Railway.Prelude;

namespace Aero.Cms.Data.Repositories;

/// <summary>
/// Defines an interface for ISiteRepository.
/// </summary>
public interface ISiteRepository
{
        /// <summary>
    /// GetAllAsync method.
    /// </summary>
Task<IEnumerable<SitesModel>> GetAllAsync(int page = 1, int num = 10, CancellationToken ct = default);
        /// <summary>
    /// FindByIdAsync method.
    /// </summary>
Task<Option<SitesModel>> FindByIdAsync(long id, CancellationToken ct = default);
        /// <summary>
    /// InsertAsync method.
    /// </summary>
Task<SitesModel> InsertAsync(SitesModel entity, CancellationToken ct = default);
        /// <summary>
    /// UpdateAsync method.
    /// </summary>
Task<SitesModel> UpdateAsync(SitesModel entity, CancellationToken ct = default);
        /// <summary>
    /// DeleteAsync method.
    /// </summary>
Task<bool> DeleteAsync(long id, CancellationToken ct = default);

        /// <summary>
    /// GetByTenantIdAsync method.
    /// </summary>
Task<IList<SitesModel>> GetByTenantIdAsync(long tenantId, CancellationToken cancellationToken = default);
        /// <summary>
    /// GetByHostnameAsync method.
    /// </summary>
Task<SitesModel?> GetByHostnameAsync(string hostname, CancellationToken cancellationToken = default);
        /// <summary>
    /// GetByNameAsync method.
    /// </summary>
Task<IList<SitesModel>> GetByNameAsync(string name, CancellationToken cancellationToken = default);
        /// <summary>
    /// GetEnabledAsync method.
    /// </summary>
Task<IList<SitesModel>> GetEnabledAsync(CancellationToken cancellationToken = default);
        /// <summary>
    /// GetDisabledAsync method.
    /// </summary>
Task<IList<SitesModel>> GetDisabledAsync(CancellationToken cancellationToken = default);
        /// <summary>
    /// GetByDefaultCultureAsync method.
    /// </summary>
Task<IList<SitesModel>> GetByDefaultCultureAsync(string defaultCulture, CancellationToken cancellationToken = default);
        /// <summary>
    /// GetCreatedInRangeAsync method.
    /// </summary>
Task<IList<SitesModel>> GetCreatedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
        /// <summary>
    /// GetModifiedInRangeAsync method.
    /// </summary>
Task<IList<SitesModel>> GetModifiedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a class for SiteRepository.
/// </summary>
public sealed class SiteRepository(IDocumentSession session, ILogger<SiteRepository> log) : ISiteRepository
{
        /// <summary>
    /// GetAllAsync method.
    /// </summary>
public async Task<IEnumerable<SitesModel>> GetAllAsync(int page = 1, int num = 10, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        var records = await session.Query<SitesModel>()
            .Skip((page - 1) * num)
            .Take(num)
            .ToListAsync(ct);
        return records?.AsEnumerable() ?? [];
    }

        /// <summary>
    /// FindByIdAsync method.
    /// </summary>
public async Task<Option<SitesModel>> FindByIdAsync(long id, CancellationToken ct = default)
    {
        var res = await session.LoadAsync<SitesModel>(id, ct);
        return res is not null ? Some(res) : None;
    }

        /// <summary>
    /// InsertAsync method.
    /// </summary>
public async Task<SitesModel> InsertAsync(SitesModel entity, CancellationToken ct = default)
    {
        session.Store(entity);
        await session.SaveChangesAsync(ct);
        return entity;
    }

        /// <summary>
    /// UpdateAsync method.
    /// </summary>
public async Task<SitesModel> UpdateAsync(SitesModel entity, CancellationToken ct = default)
    {
        session.Store(entity);
        await session.SaveChangesAsync(ct);
        return entity;
    }

        /// <summary>
    /// DeleteAsync method.
    /// </summary>
public async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        session.Delete<SitesModel>(id);
        var result = await session.SaveChangesAsync(ct)
            .ContinueWith(t => t.IsCompletedSuccessfully, ct);
        return result;
    }

        /// <summary>
    /// GetByTenantIdAsync method.
    /// </summary>
public async Task<IList<SitesModel>> GetByTenantIdAsync(long tenantId, CancellationToken cancellationToken = default)
        => await session.QueryAsync(new SitesByTenantIdQuery { TenantId = tenantId }, cancellationToken);

        /// <summary>
    /// GetByHostnameAsync method.
    /// </summary>
public async Task<SitesModel?> GetByHostnameAsync(string hostname, CancellationToken cancellationToken = default)
    {
        // hostname is expected to be pre-normalized by the caller (see SiteService)
        var siteHost = await session.QueryAsync(new SiteByHostnameQuery { hostname = hostname }, cancellationToken);
        if (siteHost is null) return null;
        return await session.LoadAsync<SitesModel>(siteHost.SiteId, cancellationToken);
    }

        /// <summary>
    /// GetByNameAsync method.
    /// </summary>
public async Task<IList<SitesModel>> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        => await session.QueryAsync(new SitesByNameQuery { Name = name }, cancellationToken);

        /// <summary>
    /// GetEnabledAsync method.
    /// </summary>
public async Task<IList<SitesModel>> GetEnabledAsync(CancellationToken cancellationToken = default)
        => await session.QueryAsync(new EnabledSitesQuery(), cancellationToken);

        /// <summary>
    /// GetDisabledAsync method.
    /// </summary>
public async Task<IList<SitesModel>> GetDisabledAsync(CancellationToken cancellationToken = default)
        => await session.QueryAsync(new DisabledSitesQuery(), cancellationToken);

        /// <summary>
    /// GetByDefaultCultureAsync method.
    /// </summary>
public async Task<IList<SitesModel>> GetByDefaultCultureAsync(string defaultCulture, CancellationToken cancellationToken = default)
        => await session.QueryAsync(new SitesByDefaultCultureQuery { DefaultCulture = defaultCulture }, cancellationToken);

        /// <summary>
    /// GetCreatedInRangeAsync method.
    /// </summary>
public async Task<IList<SitesModel>> GetCreatedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        => await session.QueryAsync(new SitesCreatedInRangeQuery { From = from, To = to }, cancellationToken);

        /// <summary>
    /// GetModifiedInRangeAsync method.
    /// </summary>
public async Task<IList<SitesModel>> GetModifiedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        => await session.QueryAsync(new SitesModifiedInRangeQuery { From = from, To = to }, cancellationToken);
}
