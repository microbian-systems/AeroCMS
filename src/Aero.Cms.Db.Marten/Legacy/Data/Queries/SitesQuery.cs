using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Queries.Base;
using Marten.Linq;
using System.Linq.Expressions;

namespace Aero.Cms.Data.Queries;

public sealed class SiteByHostnameQuery : ICompiledQuery<SiteHost, SiteHost?>
{
    public string hostname { get; set; } = null!;

    public Expression<Func<IMartenQueryable<SiteHost>, SiteHost?>> QueryIs()
    {
        return q => q.FirstOrDefault(x => x.Host == hostname);
    }
}

public sealed class SiteByIdQuery : EntityByIdQuery<SitesModel>;

public sealed class SitesByIdsQuery : EntitiesByIdsQuery<SitesModel>;

public sealed class SitesByTenantIdQuery : ICompiledQuery<SitesModel, IList<SitesModel>>
{
    public required long TenantId { get; set; }

    public Expression<Func<IMartenQueryable<SitesModel>, IList<SitesModel>>> QueryIs()
    {
        return q => q
            .Where(x => x.TenantId == TenantId)
            .OrderBy(x => x.Name)
            .ToList();
    }
}


public sealed class SitesByNameQuery : ICompiledQuery<SitesModel, IList<SitesModel>>
{
    public required string Name { get; set; }

    public Expression<Func<IMartenQueryable<SitesModel>, IList<SitesModel>>> QueryIs()
    {
        return q => q
            .Where(x => x.Name == Name)
            .OrderBy(x => x.Name)
            .ToList();
    }
}

public sealed class EnabledSitesQuery : ICompiledQuery<SitesModel, IList<SitesModel>>
{
    public Expression<Func<IMartenQueryable<SitesModel>, IList<SitesModel>>> QueryIs()
    {
        return q => q
            .Where(x => x.IsEnabled)
            .OrderBy(x => x.Name)
            .ToList();
    }
}

public sealed class DisabledSitesQuery : ICompiledQuery<SitesModel, IList<SitesModel>>
{
    public Expression<Func<IMartenQueryable<SitesModel>, IList<SitesModel>>> QueryIs()
    {
        return q => q
            .Where(x => !x.IsEnabled)
            .OrderBy(x => x.Name)
            .ToList();
    }
}

public sealed class SitesByDefaultCultureQuery : ICompiledQuery<SitesModel, IList<SitesModel>>
{
    public required string DefaultCulture { get; set; }

    public Expression<Func<IMartenQueryable<SitesModel>, IList<SitesModel>>> QueryIs()
    {
        return q => q
            .Where(x => x.DefaultCulture == DefaultCulture)
            .OrderBy(x => x.Name)
            .ToList();
    }
}

public sealed class SitesCreatedInRangeQuery : EntitiesCreatedInRangeQuery<SitesModel>;

public sealed class SitesModifiedInRangeQuery : EntitiesModifiedInRangeQuery<SitesModel>;