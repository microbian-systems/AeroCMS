using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Repositories;
using Aero.Core;
using Aero.Core.Railway;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Sites;

/// <summary>
/// Service for managing sites in the CMS.
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
}

/// <summary>
/// Implementation of site management service using Railway Oriented Programming patterns.
/// </summary>
public class SiteService(
    ISiteRepository repo, 
    ILogger<SiteService> log) : ISiteService
{
    public async Task<Result<SitesModel, AeroError>> CreateSiteAsync(SitesModel site, CancellationToken ct = default)
    {
        var validator = new SiteModelValidator();
        var result = validator.Validate(site);
        
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
        var result = validator.Validate(site);
        
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
        var site = await repo.GetByHostnameAsync(hostname, ct);
        return site;
    }
}
