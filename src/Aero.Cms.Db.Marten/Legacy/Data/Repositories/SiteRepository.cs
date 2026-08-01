using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Queries;
using Aero.Marten;
using Marten;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Data.Repositories;

/// <summary>
/// Defines an interface for ISiteRepository.
/// </summary>
public interface ISiteRepository : IMartenGenericRepositoryOption<SitesModel>
{
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
public sealed class SiteRepository : MartenGenericRepositoryOption<SitesModel>, ISiteRepository
{
    private readonly IDocumentSession _session;

        /// <summary>
    /// Initializes a new instance of the <see cref="SiteRepository"/> class.
    /// </summary>
public SiteRepository(IDocumentSession session, ILogger<SiteRepository> log) : base(session, log)
    {
        _session = session;
    }

        /// <summary>
    /// GetByTenantIdAsync method.
    /// </summary>
public async Task<IList<SitesModel>> GetByTenantIdAsync(long tenantId, CancellationToken cancellationToken = default)
        => await _session.QueryAsync(new SitesByTenantIdQuery { TenantId = tenantId }, cancellationToken);

        /// <summary>
    /// GetByHostnameAsync method.
    /// </summary>
public async Task<SitesModel?> GetByHostnameAsync(string hostname, CancellationToken cancellationToken = default)
    {
        // hostname is expected to be pre-normalized by the caller (see SiteService)
        var siteHost = await _session.QueryAsync(new SiteByHostnameQuery { hostname = hostname }, cancellationToken);
        if (siteHost is null) return null;
        return await _session.LoadAsync<SitesModel>(siteHost.SiteId, cancellationToken);
    }

        /// <summary>
    /// GetByNameAsync method.
    /// </summary>
public async Task<IList<SitesModel>> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        => await _session.QueryAsync(new SitesByNameQuery { Name = name }, cancellationToken);

        /// <summary>
    /// GetEnabledAsync method.
    /// </summary>
public async Task<IList<SitesModel>> GetEnabledAsync(CancellationToken cancellationToken = default)
        => await _session.QueryAsync(new EnabledSitesQuery(), cancellationToken);

        /// <summary>
    /// GetDisabledAsync method.
    /// </summary>
public async Task<IList<SitesModel>> GetDisabledAsync(CancellationToken cancellationToken = default)
        => await _session.QueryAsync(new DisabledSitesQuery(), cancellationToken);

        /// <summary>
    /// GetByDefaultCultureAsync method.
    /// </summary>
public async Task<IList<SitesModel>> GetByDefaultCultureAsync(string defaultCulture, CancellationToken cancellationToken = default)
        => await _session.QueryAsync(new SitesByDefaultCultureQuery { DefaultCulture = defaultCulture }, cancellationToken);

        /// <summary>
    /// GetCreatedInRangeAsync method.
    /// </summary>
public async Task<IList<SitesModel>> GetCreatedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        => await _session.QueryAsync(new SitesCreatedInRangeQuery { From = from, To = to }, cancellationToken);

        /// <summary>
    /// GetModifiedInRangeAsync method.
    /// </summary>
public async Task<IList<SitesModel>> GetModifiedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        => await _session.QueryAsync(new SitesModifiedInRangeQuery { From = from, To = to }, cancellationToken);
}
