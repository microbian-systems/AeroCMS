using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Queries;
using Aero.Cms.Data.Queries.Base;
using Aero.Marten;
using JasperFx.Core;
using Marten;
using Marten.Linq;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Data.Repositories;

public interface ISiteRepository : IMartenGenericRepositoryOption<SitesModel>
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

public sealed class SiteRepository : MartenGenericRepositoryOption<SitesModel>, ISiteRepository
{
    private readonly IDocumentSession _session;

    public SiteRepository(IDocumentSession session, ILogger<SiteRepository> log) : base(session, log)
    {
        _session = session;
    }

    public async Task<IList<SitesModel>> GetByTenantIdAsync(long tenantId, CancellationToken cancellationToken = default)
        => await _session.QueryAsync(new SitesByTenantIdQuery { TenantId = tenantId }, cancellationToken);

    public Task<SitesModel?> GetByHostnameAsync(string hostname, CancellationToken cancellationToken = default)
        => _session.QueryAsync(new SiteByHostnameQuery { hostname = hostname }, cancellationToken);

    public async Task<IList<SitesModel>> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        => await _session.QueryAsync(new SitesByNameQuery { Name = name }, cancellationToken);

    public async Task<IList<SitesModel>> GetEnabledAsync(CancellationToken cancellationToken = default)
        => await _session.QueryAsync(new EnabledSitesQuery(), cancellationToken);

    public async Task<IList<SitesModel>> GetDisabledAsync(CancellationToken cancellationToken = default)
        => await _session.QueryAsync(new DisabledSitesQuery(), cancellationToken);

    public async Task<IList<SitesModel>> GetByDefaultCultureAsync(string defaultCulture, CancellationToken cancellationToken = default)
        => await _session.QueryAsync(new SitesByDefaultCultureQuery { DefaultCulture = defaultCulture }, cancellationToken);

    public async Task<IList<SitesModel>> GetCreatedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        => await _session.QueryAsync(new SitesCreatedInRangeQuery { From = from, To = to }, cancellationToken);

    public async Task<IList<SitesModel>> GetModifiedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        => await _session.QueryAsync(new SitesModifiedInRangeQuery { From = from, To = to }, cancellationToken);
}
