using System.Security.Claims;
using Aero.Models.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Users.Areas.Api.v1;

/// <summary>
/// Maps current-principal profile, password, and avatar operations.
/// </summary>
/// <remarks>
/// The route group requires an authenticated principal. Handlers also return unauthorized when
/// no numeric name-identifier claim resolves to an Identity user. Unexpected exception messages
/// are copied into problem responses after logging.
/// </remarks>
public static class ProfileApi
{
    /// <summary>
    /// Maps the administrative current-profile endpoint group.
    /// </summary>
    /// <param name="app">The endpoint route builder receiving the group.</param>
    public static void MapProfileApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{HttpConstants.ApiPrefix}admin/profile")
            .WithTags("Admin - Profile")
            .RequireAuthorization();

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

    /// <summary>
    /// Resolves the current Identity user and returns profile data with current roles.
    /// </summary>
    /// <remarks>The supplied cancellation token is not forwarded to Identity operations.</remarks>
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

    /// <summary>
    /// Replaces the current user's email and first-name field from the profile request.
    /// </summary>
    /// <remarks>
    /// <c>DisplayName</c> is stored entirely in <c>FirstName</c>; <c>LastName</c> is not changed.
    /// Identity validation failures become a bad-request response.
    /// </remarks>
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

    /// <summary>
    /// Changes the current user's password after Identity verifies the supplied current password.
    /// </summary>
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

    /// <summary>
    /// Stores the request content directly in the current user's profile-picture data field.
    /// </summary>
    /// <remarks>
    /// This handler performs no content-type, size, data-URL, or path validation and ignores the
    /// result returned by <c>UserManager.UpdateAsync</c>. Validation and
    /// payload limits must be enforced before untrusted input reaches this endpoint.
    /// </remarks>
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

    /// <summary>
    /// Clears the current user's profile-picture data field.
    /// </summary>
    /// <remarks>The Identity update result is not inspected before success is returned.</remarks>
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

    /// <summary>
    /// Resolves the current user from a numeric <see cref="ClaimTypes.NameIdentifier"/> claim.
    /// </summary>
    /// <param name="userManager">The Identity user manager.</param>
    /// <param name="httpContextAccessor">The accessor for the active principal.</param>
    /// <returns>The matching user, or <see langword="null"/> for a missing, nonnumeric, or unknown identifier.</returns>
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
