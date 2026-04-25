using Aero.Cms.Core;
using Aero.Models.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;

namespace Aero.Cms.Modules.Identity;

public static class IdentityApi
{
    public static void MapIdentityApi(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/admin/auth").WithTags("Admin - Identity");

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
        return Results.Ok(new CurrentUserResponse(user.UserName ?? user.Email ?? "Unknown", user.Email, roles.ToArray()));
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
        if (!roles.Intersect(AeroCmsRoles.All, StringComparer.OrdinalIgnoreCase).Any())
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

    public sealed record AuthenticationConfigResponse(string AuthenticationMode);

    public sealed record CurrentUserResponse(string UserName, string? Email, IReadOnlyList<string> Roles);

    public sealed record LocalLoginRequest(string EmailOrUserName, string Password, bool RememberMe);

    public sealed record LocalLoginResponse(bool Succeeded, string Message);
}
