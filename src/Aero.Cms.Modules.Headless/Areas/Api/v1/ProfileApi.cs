using System.Security.Claims;
using Aero.Models.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Headless.Areas.Api.v1;

/// <summary>
/// Admin API for current user profile management.
/// </summary>
public static class ProfileApi
{
    /// <summary>
    /// Maps the Profile Admin API endpoints.
    /// </summary>
    public static void MapProfileApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{HttpConstants.ApiPrefix}admin/profile")
            .WithTags("Admin - Profile");

        group.MapGet("/", GetProfile)
            .WithName("GetCurrentProfile");

        group.MapPut("/", UpdateProfile)
            .WithName("UpdateCurrentProfile");

        group.MapPost("/password", UpdatePassword)
            .WithName("ChangeCurrentPassword");

        group.MapPost("/avatar", UploadAvatar)
            .WithName("UploadAvatar");

        group.MapDelete("/avatar", DeleteAvatar)
            .WithName("DeleteAvatar");
    }

    private static async Task<IResult> GetProfile(
        [FromServices] UserManager<AeroUser> userManager,
        [FromServices] IHttpContextAccessor httpContextAccessor,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(ProfileApi));
        try
        {
            var user = await GetCurrentUserAsync(userManager, httpContextAccessor);
            if (user is null) return TypedResults.Unauthorized();

            var roles = await userManager.GetRolesAsync(user);

            var profile = new UserProfile(
                user.Id,
                user.UserName ?? string.Empty,
                user.Email ?? string.Empty,
                $"{user.FirstName} {user.LastName}".Trim(),
                user.ProfilePictureDataUrl,
                roles.ToList()
            );

            return TypedResults.Ok(profile);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving profile");
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> UpdateProfile(
        [FromBody] UpdateProfileRequest request,
        [FromServices] UserManager<AeroUser> userManager,
        [FromServices] IHttpContextAccessor httpContextAccessor,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(ProfileApi));
        try
        {
            var user = await GetCurrentUserAsync(userManager, httpContextAccessor);
            if (user is null) return TypedResults.Unauthorized();

            user.Email = request.Email;
            user.FirstName = request.DisplayName; // Simplified
            user.ModifiedOn = DateTimeOffset.UtcNow;

            var result = await userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                return TypedResults.BadRequest(new { errors = result.Errors.Select(e => e.Description) });
            }

            var roles = await userManager.GetRolesAsync(user);
            var profile = new UserProfile(
                user.Id,
                user.UserName ?? string.Empty,
                user.Email,
                user.FirstName,
                user.ProfilePictureDataUrl,
                roles.ToList()
            );

            return TypedResults.Ok(profile);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating profile");
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> UpdatePassword(
        [FromBody] ChangeProfilePasswordRequest request,
        [FromServices] UserManager<AeroUser> userManager,
        [FromServices] IHttpContextAccessor httpContextAccessor,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(ProfileApi));
        try
        {
            var user = await GetCurrentUserAsync(userManager, httpContextAccessor);
            if (user is null) return TypedResults.Unauthorized();

            var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
            if (!result.Succeeded)
            {
                return TypedResults.BadRequest(new { errors = result.Errors.Select(e => e.Description) });
            }

            return TypedResults.Ok(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating password");
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> UploadAvatar(
        [FromBody] UploadAvatarRequest request,
        [FromServices] UserManager<AeroUser> userManager,
        [FromServices] IHttpContextAccessor httpContextAccessor,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(ProfileApi));
        try
        {
            var user = await GetCurrentUserAsync(userManager, httpContextAccessor);
            if (user is null) return TypedResults.Unauthorized();

            user.ProfilePictureDataUrl = request.Content; // Assuming it's a data URL or path
            user.ModifiedOn = DateTimeOffset.UtcNow;

            await userManager.UpdateAsync(user);

            var roles = await userManager.GetRolesAsync(user);
            var profile = new UserProfile(
                user.Id,
                user.UserName ?? string.Empty,
                user.Email ?? string.Empty,
                user.FirstName,
                user.ProfilePictureDataUrl,
                roles.ToList()
            );

            return TypedResults.Ok(profile);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error uploading avatar");
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> DeleteAvatar(
        [FromServices] UserManager<AeroUser> userManager,
        [FromServices] IHttpContextAccessor httpContextAccessor,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(ProfileApi));
        try
        {
            var user = await GetCurrentUserAsync(userManager, httpContextAccessor);
            if (user is null) return TypedResults.Unauthorized();

            user.ProfilePictureDataUrl = string.Empty;
            user.ModifiedOn = DateTimeOffset.UtcNow;

            await userManager.UpdateAsync(user);

            return TypedResults.Ok(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting avatar");
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<AeroUser?> GetCurrentUserAsync(UserManager<AeroUser> userManager, IHttpContextAccessor httpContextAccessor)
    {
        var userIdClaim = httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim != null && long.TryParse(userIdClaim.Value, out var userId))
        {
            return await userManager.FindByIdAsync(userId.ToString());
        }
        return null;
    }
}
