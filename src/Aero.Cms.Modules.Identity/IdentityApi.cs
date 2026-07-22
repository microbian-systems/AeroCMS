using Aero.Cms.Core;
using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Models.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;

namespace Aero.Cms.Modules.Identity;

/// <summary>
/// Maps the minimal API endpoints used by the AeroCMS administrative
/// authentication client.
/// </summary>
/// <remarks>
/// The handlers depend on the host's ASP.NET Core Identity, authentication-cookie,
/// authorization, data-protection, and AeroDB configuration. This type does not
/// configure those services.
/// </remarks>
public static class IdentityApi
{
    /// <summary>
    /// Maps the authentication configuration, current-user, local-login, and logout
    /// endpoints beneath <c>/api/v1/admin/auth</c>.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder that receives the route group.</param>
    /// <remarks>
    /// <para>The method maps the following routes:</para>
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// <c>GET /config</c> returns <c>AeroCms:Bootstrap:AuthenticationMode</c> verbatim,
    /// or <c>Local</c> when the setting is absent.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <c>GET /me</c> returns the resolved Identity user, roles, derived administrator
    /// flag and nickname, and claims whose type is exactly <c>permission</c>.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <c>POST /local/login</c> authenticates a local CMS user and can create a
    /// persistent cookie when requested.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <c>POST /logout</c> signs out the current cookie principal and returns
    /// <c>204 No Content</c>.
    /// </description>
    /// </item>
    /// </list>
    /// <para>
    /// The route group adds only the <c>Admin - Identity</c> tag. Configuration, local login,
    /// and logout are explicitly anonymous so a fallback policy cannot block authentication
    /// bootstrap or cookie clearing. The current-user endpoint explicitly requires an
    /// authenticated principal. The mapper does not attach rate-limiting, antiforgery,
    /// endpoint names, or response-metadata conventions.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="endpoints"/> is <see langword="null"/>.
    /// </exception>
    public static void MapIdentityApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapExternalIdentityAdminApi();
        endpoints.MapManagerFederationApi();
        var group = endpoints.MapGroup($"/{HttpConstants.ApiPrefix}admin/auth").WithTags("Admin - Identity");

        group.MapGet("/config", GetAuthenticationConfigAsync).AllowAnonymous();

        group.MapGet("/me", GetCurrentUserAsync)
            .RequireAuthorization();
        group.MapPost("/local/login", LocalLoginAsync)
            .AllowAnonymous();
        group.MapPost("/local/login/form", LocalLoginFormAsync)
            .WithMetadata(new RequireAntiforgeryTokenAttribute())
            .AllowAnonymous();
        group.MapPost("/recovery", RecoveryLoginAsync)
            .RequireRateLimiting(ManagerRecoveryDefaults.RateLimitPolicy)
            .WithMetadata(new RequireAntiforgeryTokenAttribute())
            .AllowAnonymous();
        group.MapPost("/logout", LogoutAsync)
            .AllowAnonymous();
    }

    /// <summary>
    /// Resolves the authenticated principal to an AeroCMS user and constructs the
    /// current-user response.
    /// </summary>
    /// <param name="httpContext">The request context containing the current principal.</param>
    /// <param name="userManager">The Identity manager used to load the user, roles, and claims.</param>
    /// <returns>
    /// <c>200 OK</c> with the current user when resolution succeeds; otherwise
    /// <c>401 Unauthorized</c>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The administrator flag is a case-insensitive check for the role name
    /// <c>Admin</c>. Permission claim types are matched case-sensitively against
    /// <c>permission</c>; their raw values, order, and duplicates are returned without
    /// local normalization. The nickname is the trimmed first and last name when either
    /// is present, and otherwise the user name.
    /// </para>
    /// <para>
    /// This handler reads user state but does not check active or soft-deleted flags,
    /// impose tenant or site boundaries, or independently validate that the existing
    /// authentication session has been revoked. Identity-store failures propagate to
    /// the host exception pipeline.
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// Attempts a password sign-in for a local AeroCMS user.
    /// </summary>
    /// <param name="request">The submitted identifier, password, and persistence preference.</param>
    /// <param name="configuration">The configuration used to determine the authentication mode.</param>
    /// <param name="userManager">The Identity manager used to find and update the user.</param>
    /// <param name="signInManager">The Identity manager used to create or clear the sign-in cookie.</param>
    /// <param name="cancellationToken">
    /// The request cancellation token. The current implementation does not observe or
    /// forward it to Identity operations.
    /// </param>
    /// <returns>
    /// <list type="bullet">
    /// <item><description><c>200 OK</c> after a successful sign-in.</description></item>
    /// <item>
    /// <description>
    /// <c>400 Bad Request</c> when local authentication is disabled or required input
    /// is blank.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <c>401 Unauthorized</c> when no user is found, the password fails, or the user
    /// is locked out.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <c>403 Forbidden</c> after signing out when the resolved user has none of the
    /// configured CMS role names.
    /// </description>
    /// </item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// <para>
    /// The identifier is trimmed. An identifier containing <c>@</c> is looked up only
    /// as an email address; every other identifier is looked up only as a user name.
    /// The CMS-role check occurs before password verification. The distinct not-found,
    /// role-denied, locked-out, and invalid-password responses can disclose account,
    /// role, or lockout state and should be considered by hosts when exposing the route.
    /// </para>
    /// <para>
    /// Password failures participate in the host-configured Identity lockout policy.
    /// <see cref="LocalLoginRequest.RememberMe"/> is passed as the sign-in persistence
    /// flag; cookie name, lifetime, security attributes, sliding expiration, and
    /// data-protection keys remain host-owned.
    /// </para>
    /// <para>
    /// The handler does not check the user's active or soft-deleted state and applies
    /// no tenant or site discriminator. After successful sign-in it writes
    /// <c>LastLoginAt</c>, but ignores the update result, so a successful response does
    /// not guarantee that timestamp persistence succeeded. Store and sign-in exceptions
    /// propagate to the host exception pipeline.
    /// </para>
    /// </remarks>
    private static async Task<IResult> LocalLoginAsync(
        LocalLoginRequest request,
        HttpContext httpContext,
        IManagerAuthenticationModeResolver modeResolver,
        UserManager<AeroUser> userManager,
        SignInManager<AeroUser> signInManager,
        ManagerAuthenticationRateLimiter rateLimiter,
        CancellationToken cancellationToken)
    {
        if (!rateLimiter.TryAcquireLocalLogin(httpContext))
            return Results.StatusCode(StatusCodes.Status429TooManyRequests);

        var modeResult = await modeResolver.ResolveAsync(cancellationToken);
        if (modeResult is not Result<ManagerAuthenticationModeResolution, AeroError>.Ok(var mode) ||
            !string.Equals(mode.EffectiveProvider,
                AuthenticationProviderSelections.Manager.Local, StringComparison.Ordinal))
        {
            return Results.Unauthorized();
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

        if (!user.IsActive || user.IsDeleted)
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

    private static async Task<IResult> LocalLoginFormAsync(
        [FromForm] LocalLoginFormRequest request,
        HttpContext httpContext,
        IManagerAuthenticationModeResolver modeResolver,
        UserManager<AeroUser> userManager,
        SignInManager<AeroUser> signInManager,
        ManagerAuthenticationRateLimiter rateLimiter,
        CancellationToken cancellationToken)
    {
        if (!rateLimiter.TryAcquireLocalLogin(httpContext))
            return Results.StatusCode(StatusCodes.Status429TooManyRequests);

        var returnUrl = GetSafeLocalReturnUrl(request.ReturnUrl);
        var failure = Results.LocalRedirect(BuildLocalFailureUrl(returnUrl));
        var modeResult = await modeResolver.ResolveAsync(cancellationToken);
        if (modeResult is not Result<ManagerAuthenticationModeResolution, AeroError>.Ok(var mode) ||
            !string.Equals(mode.EffectiveProvider,
                AuthenticationProviderSelections.Manager.Local, StringComparison.Ordinal))
            return failure;

        var identifier = request.EmailOrUserName?.Trim();
        if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(request.Password))
            return failure;

        var user = identifier.Contains('@')
            ? await userManager.FindByEmailAsync(identifier)
            : await userManager.FindByNameAsync(identifier);
        if (user is null || !user.IsActive || user.IsDeleted)
            return failure;

        var roles = await userManager.GetRolesAsync(user);
        if (!roles.Intersect(CmsRoleNames.All, StringComparer.OrdinalIgnoreCase).Any())
            return failure;

        var signIn = await signInManager.PasswordSignInAsync(
            user, request.Password, request.RememberMe, lockoutOnFailure: true);
        if (!signIn.Succeeded)
            return failure;

        user.LastLoginAt = DateTimeOffset.UtcNow;
        await userManager.UpdateAsync(user);
        return Results.LocalRedirect(returnUrl);
    }

    /// <summary>
    /// Clears the current Identity sign-in cookie.
    /// </summary>
    /// <param name="signInManager">The Identity manager that performs sign-out.</param>
    /// <returns>
    /// <c>204 No Content</c> after the sign-out operation, including when the request
    /// did not contain an authenticated principal.
    /// </returns>
    /// <remarks>
    /// Whether sign-out invalidates any other issued cookies or tokens is determined by
    /// the host and store configuration. Exceptions propagate to the host pipeline.
    /// </remarks>
    private static async Task<IResult> RecoveryLoginAsync(
        [FromForm] ManagerRecoveryLoginRequest request,
        HttpContext httpContext,
        IManagerRecoveryAuthenticationService recoveryAuthenticationService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var result = await recoveryAuthenticationService.AuthenticateAsync(
            request.EmailOrUserName,
            request.Password,
            httpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);

        if (!result.Succeeded || result.Principal is null)
        {
            return Results.LocalRedirect(BuildRecoveryFailureUrl(request.ReturnUrl));
        }

        await httpContext.SignInAsync(
            ManagerRecoveryDefaults.Scheme,
            result.Principal,
            new AuthenticationProperties
            {
                IsPersistent = false,
                AllowRefresh = false,
                ExpiresUtc = timeProvider.GetUtcNow().Add(ManagerRecoveryDefaults.SessionLifetime)
            });

        return Results.LocalRedirect(GetSafeRecoveryReturnUrl(request.ReturnUrl));
    }

    private static async Task<IResult> LogoutAsync(HttpContext httpContext)
    {
        await httpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        await httpContext.SignOutAsync(ManagerRecoveryDefaults.Scheme);
        return Results.NoContent();
    }

    private static async Task<IResult> GetAuthenticationConfigAsync(
        IManagerAuthenticationModeResolver modeResolver,
        CancellationToken cancellationToken)
    {
        var result = await modeResolver.ResolveAsync(cancellationToken);
        return result is Result<ManagerAuthenticationModeResolution, AeroError>.Ok(var mode)
            ? Results.Ok(new AuthenticationConfigResponse(mode.EffectiveProvider))
            : Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Manager authentication mode is unavailable.");
    }

    private static string GetSafeRecoveryReturnUrl(string? returnUrl)
        => IsLocalReturnUrl(returnUrl) ? returnUrl! : "/manager";

    private static string GetSafeLocalReturnUrl(string? returnUrl)
        => IsLocalReturnUrl(returnUrl) ? returnUrl! : "/manager";

    private static string BuildLocalFailureUrl(string returnUrl) =>
        $"/manager/login?error=1&returnUrl={Uri.EscapeDataString(returnUrl)}";

    private static string BuildRecoveryFailureUrl(string? returnUrl)
    {
        var safeReturnUrl = GetSafeRecoveryReturnUrl(returnUrl);
        return $"/manager/recovery?error=1&returnUrl={Uri.EscapeDataString(safeReturnUrl)}";
    }

    private static bool IsLocalReturnUrl(string? returnUrl)
        => !string.IsNullOrWhiteSpace(returnUrl)
            && returnUrl.Length <= 2048
            && returnUrl.StartsWith("/", StringComparison.Ordinal)
            && !returnUrl.StartsWith("//", StringComparison.Ordinal)
            && !returnUrl.Contains('\\')
            && returnUrl.All(character => !char.IsControl(character));

    /// <summary>
    /// Describes the authentication mode exposed to the administrative client.
    /// </summary>
    /// <param name="AuthenticationMode">
    /// The raw configured mode, or <c>Local</c> when the setting was absent.
    /// </param>
    /// <remarks>
    /// The value is informational and is not normalized or validated before being
    /// returned.
    /// </remarks>
    public sealed record AuthenticationConfigResponse(string AuthenticationMode);

    /// <summary>
    /// Describes the Identity user resolved for the current request.
    /// </summary>
    /// <param name="UserId">The user's long AeroCMS Identity key.</param>
    /// <param name="UserName">
    /// The user name, falling back to email and then <c>Unknown</c>.
    /// </param>
    /// <param name="Email">The stored email address, when present.</param>
    /// <param name="Roles">The role names returned by the configured Identity store.</param>
    /// <param name="IsAdmin">
    /// <see langword="true"/> when <c>Roles</c> contains <c>Admin</c>,
    /// ignoring case.
    /// </param>
    /// <param name="Nickname">
    /// The trimmed first-and-last-name display value, or the stored user name when both
    /// names are blank.
    /// </param>
    /// <param name="Permissions">
    /// The raw values of claims whose type is exactly <c>permission</c>.
    /// </param>
    /// <remarks>
    /// This projection does not by itself prove that the account is active, not
    /// soft-deleted, or scoped to a particular tenant or site.
    /// </remarks>
    public sealed record CurrentUserResponse(
        long UserId,
        string UserName,
        string? Email,
        IReadOnlyList<string> Roles,
        bool IsAdmin,
        string? Nickname,
        IReadOnlyList<string> Permissions);

    /// <summary>
    /// Carries the credentials and persistence preference for local sign-in.
    /// </summary>
    /// <param name="EmailOrUserName">
    /// The email address or user name to resolve. Leading and trailing whitespace is
    /// ignored by the handler.
    /// </param>
    /// <param name="Password">
    /// The plaintext password submitted for verification. Callers and intermediaries
    /// must treat this value as sensitive and avoid logging or retaining it.
    /// </param>
    /// <param name="RememberMe">
    /// Whether the resulting authentication cookie should be persistent. The host
    /// controls all other cookie behavior.
    /// </param>
    /// <remarks>
    /// This contract does not enforce HTTPS, request-size limits, rate limiting, or
    /// antiforgery protection.
    /// </remarks>
    public sealed record LocalLoginRequest(string EmailOrUserName, string Password, bool RememberMe);

    /// <summary>Browser form fields for ordinary local manager authentication.</summary>
    public sealed class LocalLoginFormRequest
    {
        public string? EmailOrUserName { get; set; }
        public string? Password { get; set; }
        public bool RememberMe { get; set; }
        public string? ReturnUrl { get; set; }
    }

    /// <summary>
    /// Reports the outcome message produced by a local-login attempt.
    /// </summary>
    /// <param name="Succeeded">
    /// <see langword="true"/> only when password sign-in completed successfully.
    /// </param>
    /// <param name="Message">
    /// A human-readable result. Failure messages may distinguish disabled local
    /// authentication, missing input, invalid credentials, and account lockout.
    /// </param>
    public sealed record LocalLoginResponse(bool Succeeded, string Message);

    /// <summary>Contains credentials for the one local recovery administrator.</summary>
    public sealed class ManagerRecoveryLoginRequest
    {
        /// <summary>Gets or sets the submitted username or email address.</summary>
        public string? EmailOrUserName { get; set; }

        /// <summary>Gets or sets the submitted recovery password.</summary>
        public string? Password { get; set; }

        /// <summary>Gets or sets the local post-authentication target.</summary>
        public string? ReturnUrl { get; set; }
    }
}
