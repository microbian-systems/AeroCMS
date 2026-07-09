using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Queries.Base;
using Marten.Linq;
using System.Linq.Expressions;

namespace Aero.Cms.Data.Queries;


public sealed class TenantByIdQuery : EntityByIdQuery<TenantModel>;

public sealed class TenantsByIdsQuery : EntitiesByIdsQuery<TenantModel>;

public sealed class TenantByNameQuery : ICompiledQuery<TenantModel, TenantModel?>
{
    public required string Name { get; set; }

    public Expression<Func<IMartenQueryable<TenantModel>, TenantModel?>> QueryIs()
    {
        return q => q
            .FirstOrDefault(x => x.Name == Name);
    }
}

public sealed class TenantByHostnameQuery : ICompiledQuery<TenantModel, TenantModel?>
{
    public required string Hostname { get; set; }

    public Expression<Func<IMartenQueryable<TenantModel>, TenantModel?>> QueryIs()
    {
        return q => q
            .FirstOrDefault(x => x.Hostname == Hostname);
    }
}

public sealed class TenantsByNotesQuery : ICompiledQuery<TenantModel, IList<TenantModel>>
{
    public required string Notes { get; set; }

    public Expression<Func<IMartenQueryable<TenantModel>, IList<TenantModel>>> QueryIs()
    {
        return q => q
            .Where(x => x.Notes == Notes)
            .OrderBy(x => x.Name)
            .ToList();
    }
}

public sealed class TenantsCreatedInRangeQuery : EntitiesCreatedInRangeQuery<TenantModel>;

public sealed class TenantsModifiedInRangeQuery : EntitiesModifiedInRangeQuery<TenantModel>;