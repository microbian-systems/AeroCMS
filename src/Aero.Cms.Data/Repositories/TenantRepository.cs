using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Queries;
using Aero.Cms.Data.Queries.Base;
using AeroDB.Sable;

namespace Aero.Cms.Data.Repositories;

/// <summary>Defines session-backed persistence and compiled-query operations for tenants.</summary>
/// <remarks>
/// Name, hostname, and notes predicates use supplied values exactly and perform no
/// normalization. Cancellation and provider failures propagate to the caller.
/// </remarks>
public interface ITenantRepository : IAeroCompiledRepository<TenantModel>
{
    /// <summary>Returns the first tenant whose stored name exactly matches a supplied value.</summary>
    /// <param name="name">The tenant name to match without normalization.</param>
    /// <param name="cancellationToken">Token forwarded to query execution.</param>
    /// <returns>The first match, or <see langword="null"/> when none exists.</returns>
Task<TenantModel?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    /// <summary>Returns the first tenant whose stored hostname exactly matches a supplied value.</summary>
    /// <param name="hostname">The hostname to match without normalization.</param>
    /// <param name="cancellationToken">Token forwarded to query execution.</param>
    /// <returns>The first match, or <see langword="null"/> when none exists.</returns>
Task<TenantModel?> GetByHostnameAsync(string hostname, CancellationToken cancellationToken = default);
    /// <summary>Returns tenants whose stored notes exactly match a supplied value.</summary>
    /// <param name="notes">The notes value to match without normalization.</param>
    /// <param name="cancellationToken">Token forwarded to query execution.</param>
    /// <returns>Matches ordered by stored tenant name, or an empty list.</returns>
Task<IList<TenantModel>> GetByNotesAsync(string notes, CancellationToken cancellationToken = default);
    /// <summary>Returns tenants created in the half-open interval from <paramref name="from"/> through, but excluding, <paramref name="to"/>.</summary>
    /// <param name="from">The inclusive creation-time lower bound.</param>
    /// <param name="to">The exclusive creation-time upper bound.</param>
    /// <param name="cancellationToken">Token forwarded to query execution.</param>
    /// <returns>The matching tenants without a guaranteed order.</returns>
Task<IList<TenantModel>> GetCreatedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
    /// <summary>Returns tenants modified in the half-open interval from <paramref name="from"/> through, but excluding, <paramref name="to"/>.</summary>
    /// <param name="from">The inclusive modification-time lower bound.</param>
    /// <param name="to">The exclusive modification-time upper bound.</param>
    /// <param name="cancellationToken">Token forwarded to query execution.</param>
    /// <returns>Matching tenants with non-null modification timestamps, without a guaranteed order.</returns>
Task<IList<TenantModel>> GetModifiedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
}

/// <summary>Executes tenant operations through a caller-owned Sable document session.</summary>
/// <param name="session">The caller-owned session used for reads and staged writes.</param>
public sealed class TenantRepository(IDocumentSession session) 
    : AeroCompiledRepository<TenantModel>(session), ITenantRepository
{

    /// <inheritdoc />
    /// <remarks>No current repository operation invokes this override.</remarks>
protected override EntitiesByIdsQuery<TenantModel> CreateByIdsQuery(IEnumerable<long> ids)
    {
        var query = new TenantsByIdsQuery()
        {
            Ids = ids
        };
        return query;
    }

    /// <inheritdoc />
public Task<TenantModel?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        => Session.QueryAsync(new TenantByNameQuery { Name = name }, cancellationToken);

    /// <inheritdoc />
public Task<TenantModel?> GetByHostnameAsync(string hostname, CancellationToken cancellationToken = default)
        => Session.QueryAsync(new TenantByHostnameQuery { Hostname = hostname }, cancellationToken);

    /// <inheritdoc />
public async Task<IList<TenantModel>> GetByNotesAsync(string notes, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new TenantsByNotesQuery { Notes = notes }, cancellationToken);

    /// <inheritdoc />
public async Task<IList<TenantModel>> GetCreatedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new TenantsCreatedInRangeQuery { From = from, To = to }, cancellationToken);

    /// <inheritdoc />
public async Task<IList<TenantModel>> GetModifiedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new TenantsModifiedInRangeQuery { From = from, To = to }, cancellationToken);
}
