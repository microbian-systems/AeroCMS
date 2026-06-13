using Aero.Cms.Core.Entities;
using Aero.Cms.Core.Infrastructure;
using Aero.Cms.Data.Repositories;
using Aero.Core;
using Aero.Core.Railway;
using Marten;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Sites;

/// <summary>
/// Service for managing sites and their host/domain assignments in the CMS.
/// </summary>
public interface ISiteService
{
    /// <summary>
    /// Creates a new site.
    /// </summary>
    Task<Result<SitesModel, AeroError>> CreateSiteAsync(SitesModel site, CancellationToken ct = default);
    
    /// <summary>
    /// Updates an existing site.
    /// </summary>
    Task<Result<SitesModel, AeroError>> UpdateSiteAsync(SitesModel site, CancellationToken ct = default);
    
    /// <summary>
    /// Deletes a site by ID.
    /// </summary>
    Task<Result<bool, AeroError>> DeleteSiteAsync(long id, CancellationToken ct = default);
    
    /// <summary>
    /// Gets a site by ID.
    /// </summary>
    Task<Option<SitesModel>> GetSiteByIdAsync(long id, CancellationToken ct = default);
    
    /// <summary>
    /// Gets all sites with pagination.
    /// </summary>
    Task<Result<IEnumerable<SitesModel>, AeroError>> GetAllSitesAsync(int page = 1, int num = 10, CancellationToken ct = default);
    
    /// <summary>
    /// Gets a site by hostname.
    /// </summary>
    Task<Option<SitesModel>> GetSiteByHostnameAsync(string hostname, CancellationToken ct = default);

    // --- Host/Domain management ---

    /// <summary>
    /// Adds a host/domain to a site. The host value is normalized before storage.
    /// </summary>
    Task<Result<SiteHost, AeroError>> AddHostAsync(long siteId, string host, bool isPrimary = false, CancellationToken ct = default);

    /// <summary>
    /// Removes a host/domain entry by its ID.
    /// </summary>
    Task<Result<bool, AeroError>> RemoveHostAsync(long hostId, CancellationToken ct = default);

    /// <summary>
    /// Gets all hosts/domains assigned to a site.
    /// </summary>
    Task<Result<IReadOnlyList<SiteHost>, AeroError>> GetHostsAsync(long siteId, CancellationToken ct = default);

    /// <summary>
    /// Replaces all hosts for a site with a new set. Atomically deletes old hosts and inserts new ones.
    /// Each entry is a tuple of (host, isPrimary).
    /// </summary>
    Task<Result<IReadOnlyList<SiteHost>, AeroError>> ReplaceHostsAsync(long siteId, List<(string host, bool isPrimary)> hosts, CancellationToken ct = default);
}

/// <summary>
/// Implementation of site management service using Railway Oriented Programming patterns.
/// </summary>
public class SiteService(
    ISiteRepository repo,
    IDocumentSession session,
    ILogger<SiteService> log) : ISiteService
{
    public async Task<Result<SitesModel, AeroError>> CreateSiteAsync(SitesModel site, CancellationToken ct = default)
    {
        var validator = new SiteModelValidator();
        var result = await validator.ValidateAsync(site);
        
        if (!result.IsValid)
        {
            var errors = result.Errors.Select(e => e.ErrorMessage).ToList();
            log.LogWarning("Site validation failed: {Errors}", string.Join(", ", errors));
            return AeroError.CreateError(string.Join("; ", errors));
        }

        try
        {
            var created = await repo.InsertAsync(site, ct);
            log.LogInformation("Created site {SiteId} with name {SiteName} for tenant {TenantId}", 
                created.Id, created.Name, created.TenantId);
            return created;
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Failed to create site");
            return AeroError.CreateError($"Failed to create site: {ex.Message}");
        }
    }

    public async Task<Result<SitesModel, AeroError>> UpdateSiteAsync(SitesModel site, CancellationToken ct = default)
    {
        var validator = new SiteModelValidator();
        var result = await validator.ValidateAsync(site);
        
        if (!result.IsValid)
        {
            var errors = result.Errors.Select(e => e.ErrorMessage).ToList();
            log.LogWarning("Site validation failed for update: {Errors}", string.Join(", ", errors));
            return AeroError.CreateError(string.Join("; ", errors));
        }

        try
        {
            var updated = await repo.UpdateAsync(site, ct);
            log.LogInformation("Updated site {SiteId}", updated.Id);
            return updated;
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Failed to update site {SiteId}", site.Id);
            return AeroError.CreateError($"Failed to update site: {ex.Message}");
        }
    }

    public async Task<Result<bool, AeroError>> DeleteSiteAsync(long id, CancellationToken ct = default)
    {
        try
        {
            // Delete all SiteHost entries first
            var hosts = await session.Query<SiteHost>()
                .Where(x => x.SiteId == id)
                .ToListAsync(ct);
            session.DeleteObjects(hosts);

            await repo.DeleteAsync(id, ct);
            log.LogInformation("Deleted site {SiteId}", id);
            return true;
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Failed to delete site {SiteId}", id);
            return AeroError.CreateError($"Failed to delete site: {ex.Message}");
        }
    }

    public async Task<Option<SitesModel>> GetSiteByIdAsync(long id, CancellationToken ct = default)
    {
        var site = await repo.FindByIdAsync(id, ct);
        return site;
    }

    public async Task<Result<IEnumerable<SitesModel>, AeroError>> GetAllSitesAsync(int page = 1, int num = 10, CancellationToken ct = default)
    {
        try
        {
            var sites = await repo.GetAllAsync(page, num, ct);
            return new Result<IEnumerable<SitesModel>, AeroError>.Ok(sites);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Failed to retrieve sites");
            return new Result<IEnumerable<SitesModel>, AeroError>.Failure(AeroError.CreateError($"Failed to retrieve sites: {ex.Message}"));
        }
    }

    public async Task<Option<SitesModel>> GetSiteByHostnameAsync(string hostname, CancellationToken ct = default)
    {
        var normalized = HostNormalizer.Normalize(hostname);
        var site = await repo.GetByHostnameAsync(normalized, ct);
        return site;
    }

    // --- Host/Domain management ---

    public async Task<Result<SiteHost, AeroError>> AddHostAsync(long siteId, string host, bool isPrimary = false, CancellationToken ct = default)
    {
        try
        {
            var normalized = HostNormalizer.Normalize(host);
            if (string.IsNullOrWhiteSpace(normalized))
                return AeroError.CreateError("Host value cannot be empty");

            var siteHost = new SiteHost
            {
                Id = Snowflake.NewId(),
                SiteId = siteId,
                Host = normalized,
                IsPrimary = isPrimary
            };

            session.Store(siteHost);
            await session.SaveChangesAsync(ct);

            log.LogInformation("Added host {Host} to site {SiteId} (primary: {IsPrimary})", normalized, siteId, isPrimary);
            return siteHost;
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Failed to add host to site {SiteId}", siteId);
            return AeroError.CreateError($"Failed to add host: {ex.Message}");
        }
    }

    public async Task<Result<bool, AeroError>> RemoveHostAsync(long hostId, CancellationToken ct = default)
    {
        try
        {
            session.Delete<SiteHost>(hostId);
            await session.SaveChangesAsync(ct);
            log.LogInformation("Removed host entry {HostId}", hostId);
            return true;
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Failed to remove host entry {HostId}", hostId);
            return AeroError.CreateError($"Failed to remove host: {ex.Message}");
        }
    }

    public async Task<Result<IReadOnlyList<SiteHost>, AeroError>> GetHostsAsync(long siteId, CancellationToken ct = default)
    {
        try
        {
            var hosts = await session.Query<SiteHost>()
                .Where(x => x.SiteId == siteId)
                .OrderByDescending(x => x.IsPrimary)
                .ThenBy(x => x.Host)
                .ToListAsync(ct);

            return new Result<IReadOnlyList<SiteHost>, AeroError>.Ok(hosts);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Failed to get hosts for site {SiteId}", siteId);
            return AeroError.CreateError($"Failed to get hosts: {ex.Message}");
        }
    }

    public async Task<Result<IReadOnlyList<SiteHost>, AeroError>> ReplaceHostsAsync(long siteId, List<(string host, bool isPrimary)> hosts, CancellationToken ct = default)
    {
        try
        {
            // Delete all existing hosts for this site
            var existing = await session.Query<SiteHost>()
                .Where(x => x.SiteId == siteId)
                .ToListAsync(ct);
            session.DeleteObjects(existing);

            // Insert new hosts
            var newHosts = hosts
                .Select(h => new SiteHost
                {
                    Id = Snowflake.NewId(),
                    SiteId = siteId,
                    Host = HostNormalizer.Normalize(h.host),
                    IsPrimary = h.isPrimary
                })
                .Where(h => !string.IsNullOrWhiteSpace(h.Host))
                .ToList();

            session.StoreObjects(newHosts);
            await session.SaveChangesAsync(ct);

            log.LogInformation("Replaced hosts for site {SiteId}: {Count} entries", siteId, newHosts.Count);
            return new Result<IReadOnlyList<SiteHost>, AeroError>.Ok(newHosts);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Failed to replace hosts for site {SiteId}", siteId);
            return AeroError.CreateError($"Failed to replace hosts: {ex.Message}");
        }
    }
}
