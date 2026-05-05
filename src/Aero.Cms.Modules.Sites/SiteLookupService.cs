using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Core.Entities;
using Aero.Cms.Core.Infrastructure;
using Aero.Marten;
using Marten;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Sites;

public sealed class SiteLookupService(IQuerySession session) : ISiteLookupService
{
    public async Task<SiteViewModel?> ResolveByHostAsync(
        string host,
        CancellationToken cancellationToken = default)
    {
        var normalized = HostNormalizer.Normalize(host);

        // Match by PrimaryHost or any entry in Hosts list
        var site = await session.Query<SitesModel>()
            .Where(x => x.IsEnabled)
            .Where(x => x.PrimaryHost == normalized || x.Hosts.Contains(normalized))
            .FirstOrDefaultAsync(cancellationToken);

        if (site is null)
            return null;

        return MapToViewModel(site);
    }

    public async Task<IReadOnlyList<SiteViewModel>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var sites = await session.Query<SitesModel>()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return sites.Select(MapToViewModel).ToList();
    }

    private static SiteViewModel MapToViewModel(SitesModel model)
    {
        return new SiteViewModel
        {
            Id = model.Id,
            TenantId = model.TenantId,
            Name = model.Name,
            PrimaryHost = model.PrimaryHost,
            Hosts = model.Hosts,
            IsEnabled = model.IsEnabled,
            DefaultCulture = model.DefaultCulture,
            CreatedOn = model.CreatedOn,
            ModifiedOn = model.ModifiedOn,
            CreatedBy = model.CreatedBy,
            ModifiedBy = model.ModifiedBy
        };
    }
}


