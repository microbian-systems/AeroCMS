using Aero.Cms.Core.Entities;
using Aero.Cms.Core.Infrastructure;
using Aero.Cms.Data.Repositories;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Sites;

/// <summary>
/// Manages site documents and their globally unique host assignments.
/// </summary>
/// <remarks>
/// This contract does not perform caller authorization or tenant scoping. Callers must validate
/// those boundaries before supplying identifiers or models.
/// </remarks>
public interface ISiteService
{
    /// <summary>
    /// Validates and inserts a site document.
    /// </summary>
    /// <param name="site">The site to validate and persist; it must already have positive site and tenant identifiers.</param>
    /// <param name="ct">The token used by persistence; the validator is invoked without this token.</param>
    /// <returns>The inserted site, or a validation or persistence failure.</returns>
    Task<Result<SitesModel, AeroError>> CreateSiteAsync(SitesModel site, CancellationToken ct = default);
    
    /// <summary>
    /// Validates and replaces an existing site document.
    /// </summary>
    /// <param name="site">The complete site state to validate and persist.</param>
    /// <param name="ct">The token used by persistence; the validator is invoked without this token.</param>
    /// <returns>The updated site, or a validation or persistence failure.</returns>
    Task<Result<SitesModel, AeroError>> UpdateSiteAsync(SitesModel site, CancellationToken ct = default);
    
    /// <summary>
    /// Deletes a site and schedules its host records for deletion.
    /// </summary>
    /// <param name="id">The persistent site identifier.</param>
    /// <param name="ct">The token used by host lookup and repository deletion.</param>
    /// <returns>
    /// A successful <see langword="true"/> value when the repository call completes, or a failure
    /// when an exception escapes persistence.
    /// </returns>
    /// <remarks>
    /// The service does not delete related user assignments or content, and it does not verify
    /// tenant ownership. With the default scoped repository registration, host cleanup and site
    /// deletion share the document session and commit together. The implementation ignores a
    /// <see langword="false"/> value returned by the repository's deletion operation.
    /// </remarks>
    Task<Result<bool, AeroError>> DeleteSiteAsync(long id, CancellationToken ct = default);
    
    /// <summary>
    /// Loads a site by its persistent identifier.
    /// </summary>
    /// <param name="id">The site identifier to load.</param>
    /// <param name="ct">The token used by the repository lookup.</param>
    /// <returns>The site when present; otherwise an empty option.</returns>
    Task<Option<SitesModel>> GetSiteByIdAsync(long id, CancellationToken ct = default);
    
    /// <summary>
    /// Returns a repository-defined page of sites.
    /// </summary>
    /// <param name="page">The one-based page number passed to the repository.</param>
    /// <param name="num">The requested page size.</param>
    /// <param name="ct">The token used by the repository query.</param>
    /// <returns>The returned site sequence, or a failure containing the query error.</returns>
    Task<Result<IEnumerable<SitesModel>, AeroError>> GetAllSitesAsync(int page = 1, int num = 10, CancellationToken ct = default);
    
    /// <summary>
    /// Resolves a normalized host name to its parent site.
    /// </summary>
    /// <param name="hostname">The host name to normalize and resolve.</param>
    /// <param name="ct">The token used by the repository query.</param>
    /// <returns>The parent site when assigned; otherwise an empty option.</returns>
    Task<Option<SitesModel>> GetSiteByHostnameAsync(string hostname, CancellationToken ct = default);

    // --- Host/Domain management ---

    /// <summary>
    /// Adds a normalized, globally unique host assignment to a site.
    /// </summary>
    /// <param name="siteId">The site identifier stored on the host record.</param>
    /// <param name="host">The host value to normalize.</param>
    /// <param name="isPrimary">Whether the assignment should be marked primary.</param>
    /// <param name="ct">The token used through lookup and commit.</param>
    /// <returns>
    /// The existing or newly stored assignment, or a validation, ownership-conflict, or persistence failure.
    /// </returns>
    /// <remarks>
    /// Re-adding a host to the same site is idempotent except that it may promote the existing
    /// record to primary. The method does not demote any other primary host or verify that
    /// <paramref name="siteId"/> exists.
    /// </remarks>
    Task<Result<SiteHost, AeroError>> AddHostAsync(long siteId, string host, bool isPrimary = false, CancellationToken ct = default);

    /// <summary>
    /// Deletes a host assignment by its document identifier.
    /// </summary>
    /// <param name="hostId">The host assignment identifier.</param>
    /// <param name="ct">The token used through the commit.</param>
    /// <returns>A successful flag, or a persistence failure.</returns>
    /// <remarks>No site-ownership or existence check is performed before deletion.</remarks>
    Task<Result<bool, AeroError>> RemoveHostAsync(long hostId, CancellationToken ct = default);

    /// <summary>
    /// Returns a site's hosts with primary entries first and then by host name.
    /// </summary>
    /// <param name="siteId">The site identifier used to filter host records.</param>
    /// <param name="ct">The token used by the query.</param>
    /// <returns>The ordered host assignments, or a query failure.</returns>
    Task<Result<IReadOnlyList<SiteHost>, AeroError>> GetHostsAsync(long siteId, CancellationToken ct = default);

    /// <summary>
    /// Replaces all host assignments for a site in one document-session commit.
    /// </summary>
    /// <param name="siteId">The site whose current assignments are replaced.</param>
    /// <param name="hosts">Candidate host values and their primary flags.</param>
    /// <param name="ct">The token used through query and commit.</param>
    /// <returns>The non-empty normalized assignments that were persisted, or a persistence failure.</returns>
    /// <remarks>
    /// Empty normalized values are discarded. The method does not deduplicate candidates, enforce
    /// a single primary entry, verify the site exists, or preflight conflicts with hosts owned by
    /// other sites; datastore constraints may reject the final commit.
    /// </remarks>
    Task<Result<IReadOnlyList<SiteHost>, AeroError>> ReplaceHostsAsync(long siteId, List<(string host, bool isPrimary)> hosts, CancellationToken ct = default);
}

/// <summary>
/// Persists sites through <see cref="ISiteRepository"/> and host assignments through a document session.
/// </summary>
/// <param name="repo">The repository used for site-document operations.</param>
/// <param name="session">The document session used for host operations.</param>
/// <param name="log">The structured operations logger.</param>
/// <remarks>
/// Expected validation and persistence errors are represented as <see cref="AeroError"/> values.
/// This implementation catches all exceptions, including cancellation, and converts them to failures.
/// </remarks>
public class SiteService(
    ISiteRepository repo,
    IDocumentSession session,
    ILogger<SiteService> log) : ISiteService
{
    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
public async Task<Option<SitesModel>> GetSiteByIdAsync(long id, CancellationToken ct = default)
    {
        var site = await repo.FindByIdAsync(id, ct);
        return site;
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
public async Task<Option<SitesModel>> GetSiteByHostnameAsync(string hostname, CancellationToken ct = default)
    {
        var normalized = HostNormalizer.Normalize(hostname);
        var site = await repo.GetByHostnameAsync(normalized, ct);
        return site;
    }

    // --- Host/Domain management ---

    /// <inheritdoc />
public async Task<Result<SiteHost, AeroError>> AddHostAsync(long siteId, string host, bool isPrimary = false, CancellationToken ct = default)
    {
        try
        {
            var normalized = HostNormalizer.Normalize(host);
            if (string.IsNullOrWhiteSpace(normalized))
                return AeroError.CreateError("Host value cannot be empty");

            var existing = await session.Query<SiteHost>()
                .Where(siteHost => siteHost.Host == normalized)
                .FirstOrDefaultAsync(ct);

            if (existing is not null)
            {
                if (existing.SiteId != siteId)
                {
                    return AeroError.CreateError(
                        $"Host '{normalized}' is already assigned to site {existing.SiteId}");
                }

                if (isPrimary && !existing.IsPrimary)
                {
                    existing.IsPrimary = true;
                    existing.ModifiedOn = DateTimeOffset.UtcNow;
                    session.Store(existing);
                    await session.SaveChangesAsync(ct);
                }

                return existing;
            }

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

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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
