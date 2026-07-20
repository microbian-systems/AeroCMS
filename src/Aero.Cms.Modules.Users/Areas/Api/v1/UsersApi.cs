using Aero.Models.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
namespace Aero.Cms.Modules.Users.Areas.Api.v1;

/// <summary>
/// Maps administrative Identity user, role, password, and site-assignment operations.
/// </summary>
/// <remarks>
/// This mapper does not attach authorization or tenant/site policies. The host must restrict
/// the route group to trusted administrators and enforce any tenant boundary externally.
/// Unexpected exception messages are copied into problem responses after logging.
/// </remarks>
public static class UsersApi
{
    /// <summary>
    /// Maps the administrative users endpoint group.
    /// </summary>
    /// <param name="app">The endpoint route builder receiving the group.</param>
    public static void MapUsersApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{HttpConstants.ApiPrefix}admin/users")
            .WithTags("Admin - Users");

        group.MapGet("/", GetAllUsers)
            .WithName("GetAllUsers");

        group.MapGet("/details/{id:long}", GetUserById)
            .WithName("GetUserById");

        group.MapPost("/", CreateUser)
            .WithName("CreateUser");

        group.MapPut("/{id:long}", UpdateUser)
            .WithName("UpdateUser");

        group.MapDelete("/{id:long}", DeleteUser)
            .WithName("DeleteUser");

        group.MapPost("/{id:long}/password", ChangePassword)
            .WithName("ChangeUserPassword");

        // User-site assignment endpoints
        group.MapGet("/{userId:long}/sites", GetUserSiteAssignments)
            .WithName("GetUserSiteAssignments");

        group.MapPut("/{userId:long}/sites", UpdateUserSiteAssignments)
            .WithName("UpdateUserSiteAssignments");
    }

    /// <summary>
    /// Executes a synchronous Identity user query with optional case-insensitive text filtering and pagination.
    /// </summary>
    /// <remarks>
    /// The handler does not clamp negative pagination values and does not use its cancellation token.
    /// Results are not tenant- or site-scoped.
    /// </remarks>
    private static async Task<IResult> GetAllUsers(
        [FromServices] UserManager<AeroUser> userManager,
        [FromServices] ILoggerFactory loggerFactory,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 10,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(UsersApi));
        try
        {
            var query = userManager.Users;

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                query = query.Where(u => 
                    (u.UserName != null && u.UserName.ToLower().Contains(s)) || 
                    (u.Email != null && u.Email.ToLower().Contains(s)) || 
                    (u.FirstName != null && u.FirstName.ToLower().Contains(s)) || 
                    (u.LastName != null && u.LastName.ToLower().Contains(s)));
            }

            var totalCount = query.Count();
            var users = query
                .OrderBy(u => u.UserName)
                .Skip(skip)
                .Take(take)
                .ToList();

            var summaries = users.Select(u => new UserSummary(
                u.Id,
                u.UserName ?? string.Empty,
                u.Email ?? string.Empty,
                $"{u.FirstName} {u.LastName}".Trim(),
                u.IsActive,
                u.CreatedOn.DateTime
            )).ToList();

            return TypedResults.Ok(new PagedResult<UserSummary>(summaries, totalCount, skip, take));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving all users");
            return TypedResults.Problem(ex.Message);
        }
    }

    /// <summary>
    /// Loads one Identity user and its current role names by Snowflake identifier.
    /// </summary>
    private static async Task<IResult> GetUserById(
        long id,
        [FromServices] UserManager<AeroUser> userManager,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(UsersApi));
        try
        {
            var user = await userManager.FindByIdAsync(id.ToString());

            if (user is null)
            {
                return TypedResults.NotFound(new { error = $"User with ID {id} not found." });
            }

            var roles = await userManager.GetRolesAsync(user);

            var detail = new UserDetail(
                user.Id,
                user.UserName ?? string.Empty,
                user.Email ?? string.Empty,
                $"{user.FirstName} {user.LastName}".Trim(),
                user.IsActive,
                user.CreatedOn.DateTime,
                user.LastLoginAt?.DateTime,
                roles.ToList()
            );

            return TypedResults.Ok(detail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving user for id={Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }

    /// <summary>
    /// Creates an active Identity user with a Snowflake identifier, then adds requested roles.
    /// </summary>
    /// <remarks>
    /// User creation and role assignment are separate operations. Role-assignment failures are
    /// ignored and do not roll back the newly created user.
    /// </remarks>
    private static async Task<IResult> CreateUser(
        [FromBody] CreateUserRequest request,
        [FromServices] UserManager<AeroUser> userManager,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(UsersApi));
        try
        {
            var user = new AeroUser
            {
                Id = Snowflake.NewId(),
                UserName = request.UserName,
                Email = request.Email,
                FirstName = request.DisplayName, // Simplified for DTO mapping
                IsActive = true,
                CreatedOn = DateTimeOffset.UtcNow
            };

            var result = await userManager.CreateAsync(user, request.Password);

            if (!result.Succeeded)
            {
                return TypedResults.BadRequest(new { errors = result.Errors.Select(e => e.Description) });
            }

            if (request.Roles.Any())
            {
                await userManager.AddToRolesAsync(user, request.Roles);
            }

            var detail = new UserDetail(
                user.Id,
                user.UserName,
                user.Email,
                user.FirstName,
                user.IsActive,
                user.CreatedOn.DateTime,
                null,
                request.Roles
            );

            return TypedResults.Ok(detail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating user");
            return TypedResults.Problem(ex.Message);
        }
    }

    /// <summary>
    /// Updates an Identity user's profile and enabled flag, then reconciles role membership.
    /// </summary>
    /// <remarks>
    /// The user update commits before role additions and removals. Role-operation failures are
    /// ignored, so the returned role list reflects the request rather than a verified persisted state.
    /// </remarks>
    private static async Task<IResult> UpdateUser(
        long id,
        [FromBody] UpdateUserRequest request,
        [FromServices] UserManager<AeroUser> userManager,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(UsersApi));
        try
        {
            var user = await userManager.FindByIdAsync(id.ToString());

            if (user is null)
            {
                return TypedResults.NotFound(new { error = $"User with ID {id} not found." });
            }

            user.Email = request.Email;
            user.FirstName = request.DisplayName;
            user.IsActive = request.IsEnabled;
            user.ModifiedOn = DateTimeOffset.UtcNow;

            var result = await userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                return TypedResults.BadRequest(new { errors = result.Errors.Select(e => e.Description) });
            }

            var currentRoles = await userManager.GetRolesAsync(user);
            var rolesToAdd = request.Roles.Except(currentRoles).ToList();
            var rolesToRemove = currentRoles.Except(request.Roles).ToList();

            if (rolesToRemove.Any()) await userManager.RemoveFromRolesAsync(user, rolesToRemove);
            if (rolesToAdd.Any()) await userManager.AddToRolesAsync(user, rolesToAdd);

            var detail = new UserDetail(
                user.Id,
                user.UserName ?? string.Empty,
                user.Email,
                user.FirstName,
                user.IsActive,
                user.CreatedOn.DateTime,
                user.LastLoginAt?.DateTime,
                request.Roles
            );

            return TypedResults.Ok(detail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating user for id={Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }

    /// <summary>
    /// Deletes an Identity user and maps Identity validation failures to a bad request.
    /// </summary>
    private static async Task<IResult> DeleteUser(
        long id,
        [FromServices] UserManager<AeroUser> userManager,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(UsersApi));
        try
        {
            var user = await userManager.FindByIdAsync(id.ToString());

            if (user is null)
            {
                return TypedResults.NotFound(new { error = $"User with ID {id} not found." });
            }

            var result = await userManager.DeleteAsync(user);

            if (!result.Succeeded)
            {
                return TypedResults.BadRequest(new { errors = result.Errors.Select(e => e.Description) });
            }

            return TypedResults.Ok(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting user for id={Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }

    /// <summary>
    /// Changes a selected user's password after validating the supplied current password.
    /// </summary>
    /// <remarks>This is not an administrator reset operation; it requires the existing password.</remarks>
    private static async Task<IResult> ChangePassword(
        long id,
        [FromBody] ChangePasswordRequest request,
        [FromServices] UserManager<AeroUser> userManager,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(UsersApi));
        try
        {
            var user = await userManager.FindByIdAsync(id.ToString());

            if (user is null)
            {
                return TypedResults.NotFound(new { error = $"User with ID {id} not found." });
            }

            var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);

            if (!result.Succeeded)
            {
                return TypedResults.BadRequest(new { errors = result.Errors.Select(e => e.Description) });
            }

            return TypedResults.Ok(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error changing password for user id={Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }

    // ── User-Site Assignment handlers ──────────────────────

    /// <summary>
    /// Lists every persisted site assignment for a user identifier.
    /// </summary>
    /// <remarks>The handler does not verify that the user exists or filter assignments by a current tenant.</remarks>
    private static async Task<IResult> GetUserSiteAssignments(
        long userId,
        IQuerySession querySession,
        CancellationToken cancellationToken,
        [FromServices] ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(typeof(UsersApi));
        try
        {
            var assignments = await querySession.Query<UserSiteAssignment>()
                .Where(a => a.UserId == userId)
                .ToListAsync(cancellationToken);

            return TypedResults.Ok(assignments.Select(a => new UserSiteAssignmentResponse(
                a.Id, a.UserId, a.SiteId, a.Permissions)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting site assignments for user id={UserId}", userId);
            return TypedResults.Problem(ex.Message);
        }
    }

    /// <summary>
    /// Replaces all persisted site assignments for a user in one document-session commit.
    /// </summary>
    /// <remarks>
    /// The handler does not verify the user, referenced sites, permissions, duplicates, or tenant
    /// ownership. Callers must validate those relationships before invoking the endpoint.
    /// </remarks>
    private static async Task<IResult> UpdateUserSiteAssignments(
        long userId,
        UserSiteAssignmentBatch request,
        IDocumentSession session,
        IQuerySession querySession,
        CancellationToken cancellationToken,
        [FromServices] ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(typeof(UsersApi));
        try
        {
            // Delete all existing assignments for this user
            var existing = await querySession.Query<UserSiteAssignment>()
                .Where(a => a.UserId == userId)
                .ToListAsync(cancellationToken);

            session.DeleteObjects(existing);

            // Create new assignments
            foreach (var item in request.Assignments)
            {
                var assignment = new UserSiteAssignment
                {
                    Id = Snowflake.NewId(),
                    UserId = userId,
                    SiteId = item.SiteId,
                    Permissions = item.Permissions?.ToList() ?? []
                };
                session.Store(assignment);
            }

            await session.SaveChangesAsync(cancellationToken);
            return TypedResults.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating site assignments for user id={UserId}", userId);
            return TypedResults.Problem(ex.Message);
        }
    }
}

/// <summary>
/// Describes one persisted user-to-site permission assignment.
/// </summary>
/// <param name="Id">The assignment's Snowflake identifier.</param>
/// <param name="UserId">The assigned user identifier.</param>
/// <param name="SiteId">The assigned site identifier.</param>
/// <param name="Permissions">The stored permission names.</param>
public record UserSiteAssignmentResponse(long Id, long UserId, long SiteId, List<string> Permissions);

/// <summary>
/// Describes one requested site and permission set in an assignment replacement.
/// </summary>
/// <param name="SiteId">The site identifier to assign.</param>
/// <param name="Permissions">The requested permission names, or <see langword="null"/> for none.</param>
public record UserSiteAssignmentItem(long SiteId, List<string>? Permissions);

/// <summary>
/// Contains the complete replacement set of site assignments for a user.
/// </summary>
/// <param name="Assignments">The assignments to store after deleting the current set.</param>
public record UserSiteAssignmentBatch(List<UserSiteAssignmentItem> Assignments);
