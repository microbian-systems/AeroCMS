using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Queries;
using Aero.Core.Railway;
using AeroDB.Sable;
using Microsoft.Extensions.Logging;
using static Aero.Core.Railway.Prelude;

namespace Aero.Cms.Data.Repositories;

/// <summary>Defines persistence and query operations for hosted sites.</summary>
/// <remarks>
/// Query methods do not normalize names, hostnames, or cultures. Cancellation and
/// underlying session failures propagate unless a member explicitly documents a
/// different result shape.
/// </remarks>
public interface ISiteRepository
{
    /// <summary>Returns one unordered page of site documents.</summary>
    /// <param name="page">The one-based page number. Values below one are treated as one.</param>
    /// <param name="num">The requested page size passed to the query provider.</param>
    /// <param name="ct">Token forwarded to query materialization.</param>
    /// <returns>The requested page, or an empty sequence when no documents are returned.</returns>
Task<IEnumerable<SitesModel>> GetAllAsync(int page = 1, int num = 10, CancellationToken ct = default);
    /// <summary>Loads a site by document identifier.</summary>
    /// <param name="id">The site document identifier.</param>
    /// <param name="ct">Token forwarded to the load operation.</param>
    /// <returns><see cref="Option{T}.Some"/> for a match; otherwise <see cref="Option{T}.None"/>.</returns>
Task<Option<SitesModel>> FindByIdAsync(long id, CancellationToken ct = default);
    /// <summary>Stores a site and persists the session changes.</summary>
    /// <param name="entity">The site document to store.</param>
    /// <param name="ct">Token forwarded to the persistence operation.</param>
    /// <returns>The same instance after persistence completes.</returns>
Task<SitesModel> InsertAsync(SitesModel entity, CancellationToken ct = default);
    /// <summary>Stores a site as an update and persists the session changes.</summary>
    /// <param name="entity">The site document to store.</param>
    /// <param name="ct">Token forwarded to the persistence operation.</param>
    /// <returns>The same instance after persistence completes.</returns>
Task<SitesModel> UpdateAsync(SitesModel entity, CancellationToken ct = default);
    /// <summary>Deletes a site identifier and attempts to persist the session changes.</summary>
    /// <param name="id">The site document identifier to delete.</param>
    /// <param name="ct">Token used by persistence and its completion continuation.</param>
    /// <returns><see langword="true"/> when persistence completes successfully; <see langword="false"/> when it faults.</returns>
Task<bool> DeleteAsync(long id, CancellationToken ct = default);

    /// <summary>Returns sites owned by one tenant.</summary>
    /// <param name="tenantId">The tenant identifier to match.</param>
    /// <param name="cancellationToken">Token forwarded to query execution.</param>
    /// <returns>Matches ordered by stored site name, or an empty list.</returns>
Task<IList<SitesModel>> GetByTenantIdAsync(long tenantId, CancellationToken cancellationToken = default);
    /// <summary>Resolves an exact, pre-normalized host value to its owning site document.</summary>
    /// <param name="hostname">The pre-normalized hostname to match without further transformation.</param>
    /// <param name="cancellationToken">Token forwarded to both the host query and site load.</param>
    /// <returns>The owning site, or <see langword="null"/> when either the host mapping or site document is absent.</returns>
Task<SitesModel?> GetByHostnameAsync(string hostname, CancellationToken cancellationToken = default);
    /// <summary>Returns sites whose stored name exactly matches a supplied value.</summary>
    /// <param name="name">The site name to match without normalization.</param>
    /// <param name="cancellationToken">Token forwarded to query execution.</param>
    /// <returns>Matches ordered by stored site name, or an empty list.</returns>
Task<IList<SitesModel>> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    /// <summary>Returns enabled sites ordered by stored site name.</summary>
    /// <param name="cancellationToken">Token forwarded to query execution.</param>
    /// <returns>Enabled sites, or an empty list.</returns>
Task<IList<SitesModel>> GetEnabledAsync(CancellationToken cancellationToken = default);
    /// <summary>Returns disabled sites ordered by stored site name.</summary>
    /// <param name="cancellationToken">Token forwarded to query execution.</param>
    /// <returns>Disabled sites, or an empty list.</returns>
Task<IList<SitesModel>> GetDisabledAsync(CancellationToken cancellationToken = default);
    /// <summary>Returns sites whose stored default culture exactly matches a supplied value.</summary>
    /// <param name="defaultCulture">The culture value to match without normalization.</param>
    /// <param name="cancellationToken">Token forwarded to query execution.</param>
    /// <returns>Matches ordered by stored site name, or an empty list.</returns>
Task<IList<SitesModel>> GetByDefaultCultureAsync(string defaultCulture, CancellationToken cancellationToken = default);
    /// <summary>Returns sites created in the half-open interval from <paramref name="from"/> through, but excluding, <paramref name="to"/>.</summary>
    /// <param name="from">The inclusive creation-time lower bound.</param>
    /// <param name="to">The exclusive creation-time upper bound.</param>
    /// <param name="cancellationToken">Token forwarded to query execution.</param>
    /// <returns>The matching sites without a guaranteed order.</returns>
Task<IList<SitesModel>> GetCreatedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
    /// <summary>Returns sites modified in the half-open interval from <paramref name="from"/> through, but excluding, <paramref name="to"/>.</summary>
    /// <param name="from">The inclusive modification-time lower bound.</param>
    /// <param name="to">The exclusive modification-time upper bound.</param>
    /// <param name="cancellationToken">Token forwarded to query execution.</param>
    /// <returns>Matching sites with non-null modification timestamps, without a guaranteed order.</returns>
Task<IList<SitesModel>> GetModifiedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
}

/// <summary>Executes site operations through one caller-owned Sable document session.</summary>
/// <param name="session">The session used for every read and write operation.</param>
/// <param name="log">The logger supplied to the repository; the current implementation does not emit repository events.</param>
public sealed class SiteRepository(IDocumentSession session, ILogger<SiteRepository> log) : ISiteRepository
{
    /// <inheritdoc />
public async Task<IEnumerable<SitesModel>> GetAllAsync(int page = 1, int num = 10, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        var records = await session.Query<SitesModel>()
            .Skip((page - 1) * num)
            .Take(num)
            .ToListAsync(ct);
        return records?.AsEnumerable() ?? [];
    }

    /// <inheritdoc />
public async Task<Option<SitesModel>> FindByIdAsync(long id, CancellationToken ct = default)
    {
        var res = await session.LoadAsync<SitesModel>(id, ct);
        return res is not null ? Some(res) : None;
    }

    /// <inheritdoc />
public async Task<SitesModel> InsertAsync(SitesModel entity, CancellationToken ct = default)
    {
        session.Store(entity);
        await session.SaveChangesAsync(ct);
        return entity;
    }

    /// <inheritdoc />
public async Task<SitesModel> UpdateAsync(SitesModel entity, CancellationToken ct = default)
    {
        session.Store(entity);
        await session.SaveChangesAsync(ct);
        return entity;
    }

    /// <inheritdoc />
public async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        session.Delete<SitesModel>(id);
        var result = await session.SaveChangesAsync(ct)
            .ContinueWith(t => t.IsCompletedSuccessfully, ct);
        return result;
    }

    /// <inheritdoc />
public async Task<IList<SitesModel>> GetByTenantIdAsync(long tenantId, CancellationToken cancellationToken = default)
        => await session.QueryAsync(new SitesByTenantIdQuery { TenantId = tenantId }, cancellationToken);

    /// <inheritdoc />
public async Task<SitesModel?> GetByHostnameAsync(string hostname, CancellationToken cancellationToken = default)
    {
        // hostname is expected to be pre-normalized by the caller (see SiteService)
        var siteHost = await session.QueryAsync(new SiteByHostnameQuery { hostname = hostname }, cancellationToken);
        if (siteHost is null) return null;
        return await session.LoadAsync<SitesModel>(siteHost.SiteId, cancellationToken);
    }

    /// <inheritdoc />
public async Task<IList<SitesModel>> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        => await session.QueryAsync(new SitesByNameQuery { Name = name }, cancellationToken);

    /// <inheritdoc />
public async Task<IList<SitesModel>> GetEnabledAsync(CancellationToken cancellationToken = default)
        => await session.QueryAsync(new EnabledSitesQuery(), cancellationToken);

    /// <inheritdoc />
public async Task<IList<SitesModel>> GetDisabledAsync(CancellationToken cancellationToken = default)
        => await session.QueryAsync(new DisabledSitesQuery(), cancellationToken);

    /// <inheritdoc />
public async Task<IList<SitesModel>> GetByDefaultCultureAsync(string defaultCulture, CancellationToken cancellationToken = default)
        => await session.QueryAsync(new SitesByDefaultCultureQuery { DefaultCulture = defaultCulture }, cancellationToken);

    /// <inheritdoc />
public async Task<IList<SitesModel>> GetCreatedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        => await session.QueryAsync(new SitesCreatedInRangeQuery { From = from, To = to }, cancellationToken);

    /// <inheritdoc />
public async Task<IList<SitesModel>> GetModifiedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        => await session.QueryAsync(new SitesModifiedInRangeQuery { From = from, To = to }, cancellationToken);
}
