using Aero.Cms.Core.Entities;
using Aero.Cms.Core.Infrastructure;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Sites;

/// <summary>
/// Resolves the manager's selected site to its persisted tenant without accepting a
/// tenant identifier from the browser.
/// </summary>
public sealed class SelectedSiteScopeResolver(IQuerySession session) : ISelectedSiteScopeResolver
{
    /// <inheritdoc />
    public async Task<SelectedSiteScope?> ResolveAsync(
        long selectedSiteId,
        CancellationToken cancellationToken = default)
    {
        if (selectedSiteId <= 0)
            return null;

        var site = await session.LoadAsync<SitesModel>(selectedSiteId, cancellationToken);
        return site is { TenantId: > 0 }
            ? new SelectedSiteScope(site.TenantId, site.Id)
            : null;
    }
}
