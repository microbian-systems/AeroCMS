using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Queries;
using Aero.Cms.Data.Queries.Base;
using Marten;

namespace Aero.Cms.Data.Repositories;

/// <summary>
/// Defines an interface for ITenantRepository.
/// </summary>
public interface ITenantRepository : IMartenCompiledRepository<TenantModel>
{
        /// <summary>
    /// GetByNameAsync method.
    /// </summary>
Task<TenantModel?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
        /// <summary>
    /// GetByHostnameAsync method.
    /// </summary>
Task<TenantModel?> GetByHostnameAsync(string hostname, CancellationToken cancellationToken = default);
        /// <summary>
    /// GetByNotesAsync method.
    /// </summary>
Task<IList<TenantModel>> GetByNotesAsync(string notes, CancellationToken cancellationToken = default);
        /// <summary>
    /// GetCreatedInRangeAsync method.
    /// </summary>
Task<IList<TenantModel>> GetCreatedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
        /// <summary>
    /// GetModifiedInRangeAsync method.
    /// </summary>
Task<IList<TenantModel>> GetModifiedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a class for TenantRepository.
/// </summary>
public sealed class TenantRepository(IDocumentSession session) 
    : MartenCompiledRepository<TenantModel>(session), ITenantRepository
{

        /// <summary>
    /// CreateByIdsQuery method.
    /// </summary>
protected override EntitiesByIdsQuery<TenantModel> CreateByIdsQuery(IEnumerable<long> ids)
    {
        var query = new TenantsByIdsQuery()
        {
            Ids = ids
        };
        return query;
    }

        /// <summary>
    /// GetByNameAsync method.
    /// </summary>
public Task<TenantModel?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        => Session.QueryAsync(new TenantByNameQuery { Name = name }, cancellationToken);

        /// <summary>
    /// GetByHostnameAsync method.
    /// </summary>
public Task<TenantModel?> GetByHostnameAsync(string hostname, CancellationToken cancellationToken = default)
        => Session.QueryAsync(new TenantByHostnameQuery { Hostname = hostname }, cancellationToken);

        /// <summary>
    /// GetByNotesAsync method.
    /// </summary>
public async Task<IList<TenantModel>> GetByNotesAsync(string notes, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new TenantsByNotesQuery { Notes = notes }, cancellationToken);

        /// <summary>
    /// GetCreatedInRangeAsync method.
    /// </summary>
public async Task<IList<TenantModel>> GetCreatedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new TenantsCreatedInRangeQuery { From = from, To = to }, cancellationToken);

        /// <summary>
    /// GetModifiedInRangeAsync method.
    /// </summary>
public async Task<IList<TenantModel>> GetModifiedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new TenantsModifiedInRangeQuery { From = from, To = to }, cancellationToken);
}
