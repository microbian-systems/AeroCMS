using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Queries;
using Aero.Core.Railway;
using AeroDB;
using Microsoft.Extensions.Logging;
using static Aero.Core.Railway.Prelude;

namespace Aero.Cms.Data.Repositories;

public interface ISiteRepository
{
    Task<IEnumerable<SitesModel>> GetAllAsync(int page = 1, int num = 10, CancellationToken ct = default);
    Task<Option<SitesModel>> FindByIdAsync(long id, CancellationToken ct = default);
    Task<SitesModel> InsertAsync(SitesModel entity, CancellationToken ct = default);
    Task<SitesModel> UpdateAsync(SitesModel entity, CancellationToken ct = default);
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);

    Task<IList<SitesModel>> GetByTenantIdAsync(long tenantId, CancellationToken cancellationToken = default);
    Task<SitesModel?> GetByHostnameAsync(string hostname, CancellationToken cancellationToken = default);
    Task<IList<SitesModel>> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<IList<SitesModel>> GetEnabledAsync(CancellationToken cancellationToken = default);
    Task<IList<SitesModel>> GetDisabledAsync(CancellationToken cancellationToken = default);
    Task<IList<SitesModel>> GetByDefaultCultureAsync(string defaultCulture, CancellationToken cancellationToken = default);
    Task<IList<SitesModel>> GetCreatedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
    Task<IList<SitesModel>> GetModifiedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
}

public sealed class SiteRepository(IDocumentSession session, ILogger<SiteRepository> log) : ISiteRepository
{
    public async Task<IEnumerable<SitesModel>> GetAllAsync(int page = 1, int num = 10, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        var records = await session.Query<SitesModel>()
            .Skip((page - 1) * num)
            .Take(num)
            .ToListAsync(ct);
        return records?.AsEnumerable() ?? [];
    }

    public async Task<Option<SitesModel>> FindByIdAsync(long id, CancellationToken ct = default)
    {
        var res = await session.LoadAsync<SitesModel>(id, ct);
        return res is not null ? Some(res) : None;
    }

    public async Task<SitesModel> InsertAsync(SitesModel entity, CancellationToken ct = default)
    {
        session.Store(entity);
        await session.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<SitesModel> UpdateAsync(SitesModel entity, CancellationToken ct = default)
    {
        session.Store(entity);
        await session.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        session.Delete<SitesModel>(id);
        var result = await session.SaveChangesAsync(ct)
            .ContinueWith(t => t.IsCompletedSuccessfully, ct);
        return result;
    }

    public async Task<IList<SitesModel>> GetByTenantIdAsync(long tenantId, CancellationToken cancellationToken = default)
        => await session.QueryAsync(new SitesByTenantIdQuery { TenantId = tenantId }, cancellationToken);

    public async Task<SitesModel?> GetByHostnameAsync(string hostname, CancellationToken cancellationToken = default)
    {
        // hostname is expected to be pre-normalized by the caller (see SiteService)
        var siteHost = await session.QueryAsync(new SiteByHostnameQuery { hostname = hostname }, cancellationToken);
        if (siteHost is null) return null;
        return await session.LoadAsync<SitesModel>(siteHost.SiteId, cancellationToken);
    }

    public async Task<IList<SitesModel>> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        => await session.QueryAsync(new SitesByNameQuery { Name = name }, cancellationToken);

    public async Task<IList<SitesModel>> GetEnabledAsync(CancellationToken cancellationToken = default)
        => await session.QueryAsync(new EnabledSitesQuery(), cancellationToken);

    public async Task<IList<SitesModel>> GetDisabledAsync(CancellationToken cancellationToken = default)
        => await session.QueryAsync(new DisabledSitesQuery(), cancellationToken);

    public async Task<IList<SitesModel>> GetByDefaultCultureAsync(string defaultCulture, CancellationToken cancellationToken = default)
        => await session.QueryAsync(new SitesByDefaultCultureQuery { DefaultCulture = defaultCulture }, cancellationToken);

    public async Task<IList<SitesModel>> GetCreatedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        => await session.QueryAsync(new SitesCreatedInRangeQuery { From = from, To = to }, cancellationToken);

    public async Task<IList<SitesModel>> GetModifiedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        => await session.QueryAsync(new SitesModifiedInRangeQuery { From = from, To = to }, cancellationToken);
}
