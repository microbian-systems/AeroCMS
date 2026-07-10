using Aero.Cms.Abstractions.Models;
using Aero.Cms.Core.Entities;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Sites;

/// <summary>
/// Manages user-to-site assignments with per-site permissions.
/// </summary>
public interface IUserSiteService
{
    /// <summary>Returns all site assignments for a user.</summary>
    Task<IReadOnlyList<UserSiteAssignment>> GetAssignmentsForUserAsync(long userId, CancellationToken ct = default);

    /// <summary>Returns all user assignments for a site.</summary>
    Task<IReadOnlyList<UserSiteAssignment>> GetAssignmentsForSiteAsync(long siteId, CancellationToken ct = default);

    /// <summary>
    /// Returns the list of sites a user can access.
    /// For admin users, returns ALL sites.
    /// For non-admin users, returns only assigned sites.
    /// </summary>
    Task<IReadOnlyList<SiteViewModel>> GetAccessibleSitesAsync(long userId, IReadOnlyList<string> roles, CancellationToken ct = default);

    /// <summary>Checks whether a user has a specific permission on a site.</summary>
    Task<bool> HasPermissionAsync(long userId, long siteId, string permission, CancellationToken ct = default);

    /// <summary>Assigns a user to a site with the given permissions.</summary>
    Task<Result<UserSiteAssignment, AeroError>> AssignUserToSiteAsync(long userId, long siteId, List<string> permissions, CancellationToken ct = default);

    /// <summary>Updates permissions for an existing assignment.</summary>
    Task<Result<UserSiteAssignment, AeroError>> UpdatePermissionsAsync(long assignmentId, List<string> permissions, CancellationToken ct = default);

    /// <summary>Removes a user assignment by its ID.</summary>
    Task<Result<bool, AeroError>> RemoveAssignmentAsync(long assignmentId, CancellationToken ct = default);

    /// <summary>Removes all assignments for a specific user+site combination.</summary>
    Task<Result<bool, AeroError>> RemoveUserFromSiteAsync(long userId, long siteId, CancellationToken ct = default);
}

/// <summary>
/// Implementation of user-site assignment service using AeroDB.
/// </summary>
public class UserSiteService(
    IDocumentSession session,
    IQuerySession querySession,
    ISiteLookupService siteLookup,
    ILogger<UserSiteService> log) : IUserSiteService
{
        /// <summary>
    /// GetAssignmentsForUserAsync method.
    /// </summary>
public async Task<IReadOnlyList<UserSiteAssignment>> GetAssignmentsForUserAsync(long userId, CancellationToken ct = default)
    {
        return await querySession.Query<UserSiteAssignment>()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.SiteId)
            .ToListAsync(ct);
    }

        /// <summary>
    /// GetAssignmentsForSiteAsync method.
    /// </summary>
public async Task<IReadOnlyList<UserSiteAssignment>> GetAssignmentsForSiteAsync(long siteId, CancellationToken ct = default)
    {
        return await querySession.Query<UserSiteAssignment>()
            .Where(x => x.SiteId == siteId)
            .OrderBy(x => x.UserId)
            .ToListAsync(ct);
    }

        /// <summary>
    /// GetAccessibleSitesAsync method.
    /// </summary>
public async Task<IReadOnlyList<SiteViewModel>> GetAccessibleSitesAsync(long userId, IReadOnlyList<string> roles, CancellationToken ct = default)
    {
        // Admin users can access all sites
        if (roles.Contains("Admin", StringComparer.OrdinalIgnoreCase))
        {
            return await siteLookup.GetAllAsync(ct);
        }

        // Non-admin users: find their assigned site IDs, then load site details
        var assignmentSiteIds = await querySession.Query<UserSiteAssignment>()
            .Where(x => x.UserId == userId)
            .Select(x => x.SiteId)
            .ToListAsync(ct);

        if (assignmentSiteIds.Count == 0)
            return [];

        var allSites = await siteLookup.GetAllAsync(ct);
        return allSites.Where(s => assignmentSiteIds.Contains(s.Id)).ToList();
    }

        /// <summary>
    /// HasPermissionAsync method.
    /// </summary>
public async Task<bool> HasPermissionAsync(long userId, long siteId, string permission, CancellationToken ct = default)
    {
        var assignment = await querySession.Query<UserSiteAssignment>()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.SiteId == siteId, ct);

        if (assignment is null)
            return false;

        return assignment.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
    }

        /// <summary>
    /// AssignUserToSiteAsync method.
    /// </summary>
public async Task<Result<UserSiteAssignment, AeroError>> AssignUserToSiteAsync(long userId, long siteId, List<string> permissions, CancellationToken ct = default)
    {
        try
        {
            // Check for existing assignment
            var existing = await querySession.Query<UserSiteAssignment>()
                .FirstOrDefaultAsync(x => x.UserId == userId && x.SiteId == siteId, ct);

            if (existing is not null)
            {
                existing.Permissions = permissions.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                session.Store(existing);
                await session.SaveChangesAsync(ct);
                log.LogInformation("Updated site assignment: user {UserId} -> site {SiteId}", userId, siteId);
                return existing;
            }

            var assignment = new UserSiteAssignment
            {
                Id = Snowflake.NewId(),
                UserId = userId,
                SiteId = siteId,
                Permissions = permissions.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            };

            session.Store(assignment);
            await session.SaveChangesAsync(ct);
            log.LogInformation("Assigned user {UserId} to site {SiteId}", userId, siteId);
            return assignment;
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Failed to assign user {UserId} to site {SiteId}", userId, siteId);
            return AeroError.CreateError($"Failed to assign user to site: {ex.Message}");
        }
    }

        /// <summary>
    /// UpdatePermissionsAsync method.
    /// </summary>
public async Task<Result<UserSiteAssignment, AeroError>> UpdatePermissionsAsync(long assignmentId, List<string> permissions, CancellationToken ct = default)
    {
        try
        {
            var assignment = await querySession.LoadAsync<UserSiteAssignment>(assignmentId, ct);
            if (assignment is null)
                return AeroError.CreateError($"Assignment {assignmentId} not found");

            assignment.Permissions = permissions.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            session.Store(assignment);
            await session.SaveChangesAsync(ct);
            log.LogInformation("Updated permissions for assignment {AssignmentId}", assignmentId);
            return assignment;
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Failed to update permissions for assignment {AssignmentId}", assignmentId);
            return AeroError.CreateError($"Failed to update permissions: {ex.Message}");
        }
    }

        /// <summary>
    /// RemoveAssignmentAsync method.
    /// </summary>
public async Task<Result<bool, AeroError>> RemoveAssignmentAsync(long assignmentId, CancellationToken ct = default)
    {
        try
        {
            session.Delete<UserSiteAssignment>(assignmentId);
            await session.SaveChangesAsync(ct);
            log.LogInformation("Removed site assignment {AssignmentId}", assignmentId);
            return true;
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Failed to remove assignment {AssignmentId}", assignmentId);
            return AeroError.CreateError($"Failed to remove assignment: {ex.Message}");
        }
    }

        /// <summary>
    /// RemoveUserFromSiteAsync method.
    /// </summary>
public async Task<Result<bool, AeroError>> RemoveUserFromSiteAsync(long userId, long siteId, CancellationToken ct = default)
    {
        try
        {
            var assignments = await querySession.Query<UserSiteAssignment>()
                .Where(x => x.UserId == userId && x.SiteId == siteId)
                .ToListAsync(ct);

            session.DeleteObjects(assignments);
            await session.SaveChangesAsync(ct);
            log.LogInformation("Removed user {UserId} from site {SiteId}", userId, siteId);
            return true;
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Failed to remove user {UserId} from site {SiteId}", userId, siteId);
            return AeroError.CreateError($"Failed to remove user from site: {ex.Message}");
        }
    }
}
