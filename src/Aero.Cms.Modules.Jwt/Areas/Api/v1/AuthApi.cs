using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Abstractions.Services;
using Aero.Cms.Core;
using Aero.Core;
using Aero.Core.Railway;
using Aero.Models.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Jwt.Areas.Api.v1;

/// <summary>
/// Maps the username/password headless and cookie sign-in endpoints.
/// </summary>
/// <remarks>
/// Authentication and cookie behavior are delegated to ASP.NET Core Identity
/// and host-registered services. This type does not configure cookie lifetime,
/// token storage, tenant scope, lockout policy, transport protection, or
/// credential redaction. Its JSON POST endpoints do not attach antiforgery
/// metadata.
/// </remarks>
public static class AuthApi
{
        /// <summary>
    /// Maps <c>POST /api/v1/auth/login</c> and
    /// <c>POST /api/v1/auth/login/cookie</c>.
    /// </summary>
    /// <param name="app">The endpoint route builder to update.</param>
    /// <remarks>
    /// The headless login looks up a user by username, checks the password with
    /// lockout-on-failure disabled, rejects inactive or deleted users, generates
    /// an API key, and returns the raw key with user and role data. On every
    /// successful login, the default API-key service replaces that user's stored
    /// digest, so a previously returned key stops validating. It does not
    /// restrict the user to a CMS role. The handler returns 200 on success, 401
    /// for missing users, password failure, or inactive/deleted users, and 500
    /// for caught exceptions; the 500 problem response exposes
    /// <see cref="Exception.Message"/>.
    ///
    /// The cookie login accepts a username or email, rejects inactive or deleted
    /// users, requires membership in a CMS role, and calls
    /// <c>PasswordSignInAsync</c> with the request's remember-me value and
    /// lockout-on-failure disabled. The host's Identity configuration determines
    /// the cookie scheme, attributes, and lifetime. A successful sign-in writes
    /// the cookie before <c>LastLoginAt</c> is saved. The subsequent
    /// <c>UserManager.UpdateAsync</c> result is ignored, so an unsuccessful
    /// update result still produces a 200 response with the cookie already
    /// issued. The handler returns 401 for missing, inactive/deleted, or failed
    /// password users; 403 for users outside the CMS roles; 200 after successful
    /// sign-in; and 500 for caught exceptions. A caught exception message is
    /// returned in the problem response, including an exception thrown after
    /// the cookie was issued.
    ///
    /// Neither JSON POST endpoint attaches an explicit authorization policy,
    /// antiforgery metadata, or rate limit. Errors log the supplied username.
    /// The headless handler forwards request cancellation only to API-key
    /// creation. The cookie handler accepts a cancellation token but does not
    /// use it; its Identity lookups, role checks, sign-in, and user update are
    /// not cancellable through that parameter.
    /// </remarks>
public static void MapAuthApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{HttpConstants.ApiPrefix}auth")
            .WithTags("Headless - Auth");

        group.MapPost("/login", Login)
            .WithName("HeadlessLogin");

        group.MapPost("/login/cookie", LoginWithCookie)
            .WithName("HeadlessLoginWithCookie");
    }

    /// <summary>
    /// Attempts an ASP.NET Core Identity password sign-in using the host's
    /// configured application-cookie scheme.
    /// </summary>
    /// <remarks>
    /// A successful <c>PasswordSignInAsync</c> call writes the configured
    /// authentication cookie through the current HTTP response. The cookie name,
    /// attributes, persistence behavior, and lifetime are controlled outside
    /// this endpoint. The later <c>UserManager.UpdateAsync</c> result is ignored,
    /// and the endpoint's cancellation-token parameter is unused. The JSON POST
    /// has no antiforgery metadata.
    /// </remarks>
    private static async Task<IResult> LoginWithCookie(
        [FromBody] LoginRequest request,
        [FromServices] SignInManager<AeroUser> signInManager,
        [FromServices] UserManager<AeroUser> userManager,
        [FromServices] IManagerAuthenticationModeResolver modeResolver,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(typeof(AuthApi));

        try
        {
            if (!await IsLocalManagerAuthenticationEnabledAsync(modeResolver, cancellationToken))
            {
                return TypedResults.Json(
                    new { message = "Invalid credentials." },
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            // Support both email and username lookup (matching IdentityApi pattern)
            var user = request.UserName.Contains('@')
                ? await userManager.FindByEmailAsync(request.UserName)
                : await userManager.FindByNameAsync(request.UserName);

            if (user == null)
            {
                return TypedResults.Json(
                    new { message = "Invalid credentials." },
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            if (!user.IsActive || user.IsDeleted)
            {
                return TypedResults.Json(
                    new { message = "Invalid credentials." },
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            // Enforce CMS role membership before setting cookie
            var roles = await userManager.GetRolesAsync(user);
            if (!roles.Intersect(CmsRoleNames.All, StringComparer.OrdinalIgnoreCase).Any())
            {
                return TypedResults.Forbid();
            }

            // PasswordSignInAsync validates the password AND sets the auth cookie
            // via HttpContext.SignInAsync if successful.
            var signInResult = await signInManager.PasswordSignInAsync(
                user, request.Password, request.RememberMe, lockoutOnFailure: false);

            if (!signInResult.Succeeded)
            {
                return TypedResults.Json(
                    new { message = "Invalid credentials." },
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            user.LastLoginAt = DateTimeOffset.UtcNow;
            await userManager.UpdateAsync(user);

            return TypedResults.Ok(new { succeeded = true });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during cookie login for user={UserName}", request.UserName);
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> Login(
        [FromBody] LoginRequest request,
        [FromServices] SignInManager<AeroUser> signInManager,
        [FromServices] UserManager<AeroUser> userManager,
        [FromServices] IApiKeyService apiKeyService,
        [FromServices] IManagerAuthenticationModeResolver modeResolver,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(typeof(AuthApi));

        try
        {
            if (!await IsLocalManagerAuthenticationEnabledAsync(modeResolver, cancellationToken))
            {
                return TypedResults.Unauthorized();
            }

            // Step 1: Validate username/password credentials
            var user = await userManager.FindByNameAsync(request.UserName);
            if (user == null)
            {
                return TypedResults.Unauthorized();
            }

            var signInResult = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: false);
            if (!signInResult.Succeeded)
            {
                return TypedResults.Unauthorized();
            }

            if (!user.IsActive || user.IsDeleted)
            {
                return TypedResults.Unauthorized();
            }

            // Step 2: Generate a key and upsert its digest, replacing any prior digest for the user.
            var apiKey = await apiKeyService.CreateKeyAsync(user.Id, user.Email!, cancellationToken: cancellationToken);

            // Step 3: Get user roles
             var roles = (await userManager.GetRolesAsync(user)).ToList();

            // Step 4: Return user info with API key
            return TypedResults.Ok(new AuthLoginResponse(
                user.Id,
                user.UserName ?? string.Empty,
                user.Email ?? string.Empty,
                $"{user.FirstName} {user.MiddleName} {user.LastName}".Trim(),
                roles,
                apiKey));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during headless login for user={UserName}", request.UserName);
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<bool> IsLocalManagerAuthenticationEnabledAsync(
        IManagerAuthenticationModeResolver modeResolver,
        CancellationToken cancellationToken)
    {
        var result = await modeResolver.ResolveAsync(cancellationToken);
        return result is Result<ManagerAuthenticationModeResolution, AeroError>.Ok(var mode) &&
               string.Equals(mode.EffectiveProvider,
                   AuthenticationProviderSelections.Manager.Local, StringComparison.Ordinal);
    }
}
