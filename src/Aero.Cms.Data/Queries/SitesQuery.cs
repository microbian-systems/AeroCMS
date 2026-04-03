using Aero.Cms.Abstractions.Models;
using Aero.Cms.Core.Entities;
using Aero.Core.Entities;
using Marten.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Aero.Cms.Data.Queries;

public record SiteContext(SitesModel Site, TenantModel? Tenant);

public sealed class SiteByHostnameQuery : ICompiledQuery<SitesModel, SitesModel?>
{
    public string Hostname { get; set; } = null!;

    public IList<TenantModel> Tenants { get; } = new List<TenantModel>();

    public Expression<Func<IMartenQueryable<SitesModel>, SitesModel?>> QueryIs()
    {
        return q => q
            .Include(x => x.TenantId, Tenants)
            .FirstOrDefault(x => x.Hostname == Hostname);
    }
}
