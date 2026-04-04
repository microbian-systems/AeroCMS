using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Queries;
using Aero.Cms.Data.Queries.Base;
using JasperFx.Core;
using Marten;
using Marten.Linq;

namespace Aero.Cms.Data.Repositories;

public interface ISiteRepository : IMartenCompiledRepository<SitesModel>
{
    Task<IList<SitesModel>> GetByTenantIdAsync(long tenantId, CancellationToken cancellationToken = default);
    Task<SitesModel?> GetByHostnameAsync(string hostname, CancellationToken cancellationToken = default);
    Task<IList<SitesModel>> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<IList<SitesModel>> GetEnabledAsync(CancellationToken cancellationToken = default);
    Task<IList<SitesModel>> GetDisabledAsync(CancellationToken cancellationToken = default);
    Task<IList<SitesModel>> GetByDefaultCultureAsync(string defaultCulture, CancellationToken cancellationToken = default);
    Task<IList<SitesModel>> GetCreatedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
    Task<IList<SitesModel>> GetModifiedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
}

public sealed class SiteRepository : MartenCompiledRepository<SitesModel>, ISiteRepository
{
    public SiteRepository(IDocumentSession session) : base(session)
    {
    }

    protected override EntityByIdQuery<SitesModel> CreateByIdQuery(long id)
        => new SiteByIdQuery { Id = id };

    protected override EntitiesByIdsQuery<SitesModel> CreateByIdsQuery(IEnumerable<long> ids)
    {
        var query = new SitesByIdsQuery()
        {
            Ids = ids
        };
        return query;
    }

    public async Task<IList<SitesModel>> GetByTenantIdAsync(long tenantId, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new SitesByTenantIdQuery { TenantId = tenantId }, cancellationToken);

    public Task<SitesModel?> GetByHostnameAsync(string hostname, CancellationToken cancellationToken = default)
        => Session.QueryAsync(new SiteByHostnameQuery { hostname = hostname }, cancellationToken);

    public async Task<IList<SitesModel>> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new SitesByNameQuery { Name = name }, cancellationToken);

    public async Task<IList<SitesModel>> GetEnabledAsync(CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new EnabledSitesQuery(), cancellationToken);

    public async Task<IList<SitesModel>> GetDisabledAsync(CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new DisabledSitesQuery(), cancellationToken);

    public async Task<IList<SitesModel>> GetByDefaultCultureAsync(string defaultCulture, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new SitesByDefaultCultureQuery { DefaultCulture = defaultCulture }, cancellationToken);

    public async Task<IList<SitesModel>> GetCreatedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new SitesCreatedInRangeQuery { From = from, To = to }, cancellationToken);

    public async Task<IList<SitesModel>> GetModifiedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new SitesModifiedInRangeQuery { From = from, To = to }, cancellationToken);
}
