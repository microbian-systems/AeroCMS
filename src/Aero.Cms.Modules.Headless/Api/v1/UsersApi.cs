using Aero.Cms.Abstractions.Http.Clients;
using Aero.Core.Identity;
using Aero.Models.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Headless.Api.v1;

/// <summary>
/// Admin API for user management.
/// </summary>
public static class UsersApi
{
    /// <summary>
    /// Maps the Users Admin API endpoints.
    /// </summary>
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
    }

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

            var totalCount = await EntityFrameworkQueryableExtensions.CountAsync(query, cancellationToken);
            var users = await EntityFrameworkQueryableExtensions.ToListAsync(query
                .OrderBy(u => u.UserName)
                .Skip(skip)
                .Take(take), cancellationToken);

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
}
