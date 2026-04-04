using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Queries;
using Aero.Cms.Data.Queries.Base;
using JasperFx.Core;
using Marten;
using Marten.Linq;

namespace Aero.Cms.Data.Repositories;

public interface ITenantRepository : IMartenCompiledRepository<TenantModel>
{
    Task<TenantModel?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<TenantModel?> GetByHostnameAsync(string hostname, CancellationToken cancellationToken = default);
    Task<IList<TenantModel>> GetByNotesAsync(string notes, CancellationToken cancellationToken = default);
    Task<IList<TenantModel>> GetCreatedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
    Task<IList<TenantModel>> GetModifiedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
}

public sealed class TenantRepository(IDocumentSession session) 
    : MartenCompiledRepository<TenantModel>(session), ITenantRepository
{

    protected override EntitiesByIdsQuery<TenantModel> CreateByIdsQuery(IEnumerable<long> ids)
    {
        var query = new TenantsByIdsQuery()
        {
            Ids = ids
        };
        return query;
    }

    public Task<TenantModel?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        => Session.QueryAsync(new TenantByNameQuery { Name = name }, cancellationToken);

    public Task<TenantModel?> GetByHostnameAsync(string hostname, CancellationToken cancellationToken = default)
        => Session.QueryAsync(new TenantByHostnameQuery { Hostname = hostname }, cancellationToken);

    public async Task<IList<TenantModel>> GetByNotesAsync(string notes, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new TenantsByNotesQuery { Notes = notes }, cancellationToken);

    public async Task<IList<TenantModel>> GetCreatedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new TenantsCreatedInRangeQuery { From = from, To = to }, cancellationToken);

    public async Task<IList<TenantModel>> GetModifiedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new TenantsModifiedInRangeQuery { From = from, To = to }, cancellationToken);
}
