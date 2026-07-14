using Aero.Cms.Core;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Models.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;

namespace Aero.Cms.Modules.Identity;

/// <summary>
/// Represents a class for IdentityApi.
/// </summary>
public static class IdentityApi
{
        /// <summary>
    /// MapIdentityApi method.
    /// </summary>
public static void MapIdentityApi(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup($"/{HttpConstants.ApiPrefix}admin/auth").WithTags("Admin - Identity");

        group.MapGet("/config", (IConfiguration configuration) =>
        {
            var authenticationMode = configuration["AeroCms:Bootstrap:AuthenticationMode"] ?? "Local";
            return Results.Ok(new AuthenticationConfigResponse(authenticationMode));
        });

        group.MapGet("/me", GetCurrentUserAsync);
        group.MapPost("/local/login", LocalLoginAsync);
        group.MapPost("/logout", LogoutAsync);
    }

    private static async Task<IResult> GetCurrentUserAsync(HttpContext httpContext, UserManager<AeroUser> userManager)
    {
        if (httpContext.User.Identity?.IsAuthenticated != true)
        {
            return Results.Unauthorized();
        }

        var user = await userManager.GetUserAsync(httpContext.User);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var roles = await userManager.GetRolesAsync(user);
        var isAdmin = roles.Contains("Admin", StringComparer.OrdinalIgnoreCase);
        var nickName = string.IsNullOrWhiteSpace($"{user.FirstName} {user.LastName}".Trim())
            ? user.UserName
            : $"{user.FirstName} {user.LastName}".Trim();
        var permissions = await userManager.GetClaimsAsync(user);
        var permValues = permissions
            .Where(c => c.Type == "permission")
            .Select(c => c.Value)
            .ToList();
        return Results.Ok(new CurrentUserResponse(
            user.Id,
            user.UserName ?? user.Email ?? "Unknown",
            user.Email,
            roles.ToArray(),
            isAdmin,
            nickName,
            permValues));
    }

    private static async Task<IResult> LocalLoginAsync(
        LocalLoginRequest request,
        IConfiguration configuration,
        UserManager<AeroUser> userManager,
        SignInManager<AeroUser> signInManager,
        CancellationToken cancellationToken)
    {
        var authenticationMode = configuration["AeroCms:Bootstrap:AuthenticationMode"] ?? "Local";
        if (!string.Equals(authenticationMode, "Local", StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new LocalLoginResponse(false,
                "Local authentication is disabled for this installation."));
        }

        var identifier = request.EmailOrUserName?.Trim();
        if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.BadRequest(new LocalLoginResponse(false, "Username/email and password are required."));
        }

        var user = identifier.Contains('@')
            ? await userManager.FindByEmailAsync(identifier)
            : await userManager.FindByNameAsync(identifier);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var roles = await userManager.GetRolesAsync(user);
        if (!roles.Intersect(CmsRoleNames.All, StringComparer.OrdinalIgnoreCase).Any())
        {
            await signInManager.SignOutAsync();
            return Results.Forbid();
        }

        var result = await signInManager.PasswordSignInAsync(user, request.Password, request.RememberMe,
            lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            var message = result.IsLockedOut
                ? "This account is locked. Try again later."
                : "Invalid username/email or password.";
            return Results.Json(new LocalLoginResponse(false, message), statusCode: StatusCodes.Status401Unauthorized);
        }

        user.LastLoginAt = DateTimeOffset.UtcNow;
        await userManager.UpdateAsync(user);

        return Results.Ok(new LocalLoginResponse(true, "Login successful."));
    }

    private static async Task<IResult> LogoutAsync(SignInManager<AeroUser> signInManager)
    {
        await signInManager.SignOutAsync();
        return Results.NoContent();
    }

        /// <summary>
    /// Represents a record for AuthenticationConfigResponse.
    /// </summary>
public sealed record AuthenticationConfigResponse(string AuthenticationMode);

        /// <summary>
    /// Represents a record for CurrentUserResponse.
    /// </summary>
public sealed record CurrentUserResponse(
        long UserId,
        string UserName,
        string? Email,
        IReadOnlyList<string> Roles,
        bool IsAdmin,
        string? Nickname,
        IReadOnlyList<string> Permissions);

        /// <summary>
    /// Represents a record for LocalLoginRequest.
    /// </summary>
public sealed record LocalLoginRequest(string EmailOrUserName, string Password, bool RememberMe);

        /// <summary>
    /// Represents a record for LocalLoginResponse.
    /// </summary>
public sealed record LocalLoginResponse(bool Succeeded, string Message);
}
