using Aero.Cms.Abstractions.Models;
using Aero.Cms.Core.Entities;
using Aero.Cms.Core.Infrastructure;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Sites;

/// <summary>
/// Represents a class for SiteLookupService.
/// </summary>
public sealed class SiteLookupService(IQuerySession session) : ISiteLookupService
{
        /// <summary>
    /// ResolveByHostAsync method.
    /// </summary>
public async Task<SiteViewModel?> ResolveByHostAsync(
        string host,
        CancellationToken cancellationToken = default)
    {
        var normalized = HostNormalizer.Normalize(host);

        // Query SiteHost first — it has a unique btree index on Host for fast lookup
        var siteHost = await session.Query<SiteHost>()
            .FirstOrDefaultAsync(x => x.Host == normalized, cancellationToken);

        if (siteHost is null)
            return null;

        // Load the parent site
        var site = await session.LoadAsync<SitesModel>(siteHost.SiteId, cancellationToken);
        if (site is null || !site.IsEnabled)
            return null;

        // Load all hosts for this site to populate the view model
        var allHosts = await session.Query<SiteHost>()
            .Where(x => x.SiteId == site.Id)
            .ToListAsync(cancellationToken);

        return MapToViewModel(site, allHosts);
    }

        /// <summary>
    /// GetAllAsync method.
    /// </summary>
public async Task<IReadOnlyList<SiteViewModel>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var sites = await session.Query<SitesModel>()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        if (sites.Count == 0)
            return [];

        // Batch-load all SiteHost records for the returned sites
        var siteIds = sites.Select(s => s.Id).ToList();
        var allHosts = await session.Query<SiteHost>()
            .Where(x => siteIds.Contains(x.SiteId))
            .ToListAsync(cancellationToken);

        var hostsBySite = allHosts
            .GroupBy(h => h.SiteId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return sites
            .Select(s => MapToViewModel(s, hostsBySite.GetValueOrDefault(s.Id, [])))
            .ToList();
    }

    private static SiteViewModel MapToViewModel(SitesModel model, IReadOnlyList<SiteHost> hosts)
    {
        return new SiteViewModel
        {
            Id = model.Id,
            TenantId = model.TenantId,
            Name = model.Name,
            PrimaryHost = hosts.FirstOrDefault(h => h.IsPrimary)?.Host ?? hosts.FirstOrDefault()?.Host,
            Hosts = hosts.Select(h => h.Host).ToList(),
            IsEnabled = model.IsEnabled,
            DefaultCulture = model.DefaultCulture,
            SupportedCultures = model.SupportedCultures,
            CreatedOn = model.CreatedOn,
            ModifiedOn = model.ModifiedOn,
            CreatedBy = model.CreatedBy,
            ModifiedBy = model.ModifiedBy
        };
    }
}


