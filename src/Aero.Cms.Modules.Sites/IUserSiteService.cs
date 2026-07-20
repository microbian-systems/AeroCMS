using Aero.Cms.Abstractions.Models;
using Aero.Cms.Core.Entities;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Sites;

/// <summary>
/// Reads and mutates user-to-site assignments and their permission strings.
/// </summary>
/// <remarks>
/// The service does not authenticate callers or validate that users and sites exist. Callers must
/// enforce administrative authorization and the intended tenant boundary.
/// </remarks>
public interface IUserSiteService
{
    /// <summary>Returns a user's assignments ordered by site identifier.</summary>
    /// <param name="userId">The user identifier used to filter assignments.</param>
    /// <param name="ct">The token used by the query.</param>
    /// <returns>All matching assignments.</returns>
    Task<IReadOnlyList<UserSiteAssignment>> GetAssignmentsForUserAsync(long userId, CancellationToken ct = default);

    /// <summary>Returns a site's assignments ordered by user identifier.</summary>
    /// <param name="siteId">The site identifier used to filter assignments.</param>
    /// <param name="ct">The token used by the query.</param>
    /// <returns>All matching assignments.</returns>
    Task<IReadOnlyList<UserSiteAssignment>> GetAssignmentsForSiteAsync(long siteId, CancellationToken ct = default);

    /// <summary>
    /// Returns the site views accessible to a user.
    /// </summary>
    /// <param name="userId">The user whose assignments are evaluated.</param>
    /// <param name="roles">The caller-supplied role names for the user.</param>
    /// <param name="ct">The token used by assignment and site queries.</param>
    /// <returns>
    /// Every site when <paramref name="roles"/> contains <c>Admin</c> case-insensitively; otherwise
    /// only sites whose identifiers appear in the user's assignments.
    /// </returns>
    /// <remarks>The method trusts the supplied role list and does not filter disabled sites.</remarks>
    Task<IReadOnlyList<SiteViewModel>> GetAccessibleSitesAsync(long userId, IReadOnlyList<string> roles, CancellationToken ct = default);

    /// <summary>Checks an assignment for a case-insensitive permission match.</summary>
    /// <param name="userId">The assigned user identifier.</param>
    /// <param name="siteId">The assigned site identifier.</param>
    /// <param name="permission">The permission string to compare.</param>
    /// <param name="ct">The token used by the assignment query.</param>
    /// <returns><see langword="true"/> when a matching assignment contains the permission.</returns>
    Task<bool> HasPermissionAsync(long userId, long siteId, string permission, CancellationToken ct = default);

    /// <summary>Creates an assignment or replaces the permissions on an existing user-site assignment.</summary>
    /// <param name="userId">The user identifier stored on a new assignment.</param>
    /// <param name="siteId">The site identifier stored on a new assignment.</param>
    /// <param name="permissions">Permission strings deduplicated case-insensitively while preserving first occurrences.</param>
    /// <param name="ct">The token used through lookup and commit.</param>
    /// <returns>The persisted assignment, or a persistence failure.</returns>
    Task<Result<UserSiteAssignment, AeroError>> AssignUserToSiteAsync(long userId, long siteId, List<string> permissions, CancellationToken ct = default);

    /// <summary>Replaces the permission set on an assignment loaded by identifier.</summary>
    /// <param name="assignmentId">The assignment document identifier.</param>
    /// <param name="permissions">Permission strings deduplicated case-insensitively.</param>
    /// <param name="ct">The token used through lookup and commit.</param>
    /// <returns>The updated assignment, or a not-found or persistence failure.</returns>
    Task<Result<UserSiteAssignment, AeroError>> UpdatePermissionsAsync(long assignmentId, List<string> permissions, CancellationToken ct = default);

    /// <summary>Deletes an assignment by identifier.</summary>
    /// <param name="assignmentId">The assignment document identifier.</param>
    /// <param name="ct">The token used through the commit.</param>
    /// <returns>A successful flag, including when no matching document exists, or a persistence failure.</returns>
    Task<Result<bool, AeroError>> RemoveAssignmentAsync(long assignmentId, CancellationToken ct = default);

    /// <summary>Deletes every assignment matching a user-site pair.</summary>
    /// <param name="userId">The user identifier used to filter assignments.</param>
    /// <param name="siteId">The site identifier used to filter assignments.</param>
    /// <param name="ct">The token used through lookup and commit.</param>
    /// <returns>A successful flag, including when no assignments match, or a persistence failure.</returns>
    Task<Result<bool, AeroError>> RemoveUserFromSiteAsync(long userId, long siteId, CancellationToken ct = default);
}

/// <summary>
/// Implements assignment queries and mutations with separate query and document sessions.
/// </summary>
/// <param name="session">The session used to store and delete assignments.</param>
/// <param name="querySession">The session used to load and filter assignments.</param>
/// <param name="siteLookup">The service used to expand assignments into site views.</param>
/// <param name="log">The structured mutation logger.</param>
/// <remarks>
/// Read methods allow query and cancellation exceptions to propagate. Mutation methods log and
/// convert all exceptions, including cancellation, to <see cref="AeroError"/> failures.
/// </remarks>
public class UserSiteService(
    IDocumentSession session,
    IQuerySession querySession,
    ISiteLookupService siteLookup,
    ILogger<UserSiteService> log) : IUserSiteService
{
    /// <inheritdoc />
public async Task<IReadOnlyList<UserSiteAssignment>> GetAssignmentsForUserAsync(long userId, CancellationToken ct = default)
    {
        return await querySession.Query<UserSiteAssignment>()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.SiteId)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
public async Task<IReadOnlyList<UserSiteAssignment>> GetAssignmentsForSiteAsync(long siteId, CancellationToken ct = default)
    {
        return await querySession.Query<UserSiteAssignment>()
            .Where(x => x.SiteId == siteId)
            .OrderBy(x => x.UserId)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
public async Task<bool> HasPermissionAsync(long userId, long siteId, string permission, CancellationToken ct = default)
    {
        var assignment = await querySession.Query<UserSiteAssignment>()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.SiteId == siteId, ct);

        if (assignment is null)
            return false;

        return assignment.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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
