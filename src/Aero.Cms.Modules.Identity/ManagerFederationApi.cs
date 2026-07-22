using System.Globalization;
using System.Security.Claims;
using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Core;
using Aero.Cms.Core.Entities;
using Aero.Core;
using Aero.Core.Http;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Core.Railway;
using Aero.Models.Entities;
using AeroDB.Sable;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Identity;

/// <summary>Maps installation-wide CMS manager federation begin and callback routes.</summary>
public static class ManagerFederationApi
{
    private const string FailurePath = "/manager/login?error=1";

    /// <summary>Maps the manager federation endpoints.</summary>
    public static void MapManagerFederationApi(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup($"/{HttpConstants.ApiPrefix}admin/auth/federation")
            .WithTags("Admin - Identity");

        group.MapGet("/login", BeginLoginAsync)
            .AllowAnonymous();
        group.MapGet("/authority", GetAuthorityAsync)
            .RequireAuthorization("AeroAdmin");
        group.MapPut("/authority", ConfigureAuthorityAsync)
            .RequireAuthorization("AeroAdmin")
            .WithMetadata(new RequireAntiforgeryTokenAttribute());
        group.MapPost("/authority/form", ConfigureAuthorityFormAsync)
            .RequireAuthorization("AeroAdmin")
            .WithMetadata(new RequireAntiforgeryTokenAttribute());
        group.MapPost("/link", BeginLinkAsync)
            .RequireAuthorization("AeroAdmin")
            .WithMetadata(new RequireAntiforgeryTokenAttribute());
        group.MapPost("/logout", LogoutAsync)
            .RequireAuthorization(ManagerRecoveryDefaults.ManagerPolicy)
            .WithMetadata(new RequireAntiforgeryTokenAttribute());
        endpoints.MapGet(ManagerFederationRoutes.EntraWorkforceCallbackPath, (
                HttpContext context,
                IManagerIdentityAuthorityService authorityService,
                IManagerFederationCoordinator coordinator,
                IDocumentStore store,
                UserManager<AeroUser> userManager,
                SignInManager<AeroUser> signInManager,
                TimeProvider timeProvider,
                CancellationToken cancellationToken) =>
            CompleteCallbackAsync(ManagerIdentityProviders.EntraWorkforce,
                ManagerFederationRoutes.EntraWorkforceCallbackPath,
                context, authorityService, coordinator, store, userManager, signInManager, timeProvider, cancellationToken))
            .AllowAnonymous()
            .WithTags("Admin - Identity");
        endpoints.MapGet(ManagerFederationRoutes.WorkOsCallbackPath, (
                HttpContext context,
                IManagerIdentityAuthorityService authorityService,
                IManagerFederationCoordinator coordinator,
                IDocumentStore store,
                UserManager<AeroUser> userManager,
                SignInManager<AeroUser> signInManager,
                TimeProvider timeProvider,
                CancellationToken cancellationToken) =>
            CompleteCallbackAsync(ManagerIdentityProviders.WorkOs,
                ManagerFederationRoutes.WorkOsCallbackPath,
                context, authorityService, coordinator, store, userManager, signInManager, timeProvider, cancellationToken))
            .AllowAnonymous()
            .WithTags("Admin - Identity");
    }

    private static async Task<IResult> GetAuthorityAsync(
        IManagerIdentityAuthorityService authorityService,
        CancellationToken cancellationToken)
    {
        var result = await authorityService.GetAsync(cancellationToken);
        return result switch
        {
            Result<ManagerIdentityAuthorityResult, AeroError>.Ok(var authority) => Results.Ok(authority),
            Result<ManagerIdentityAuthorityResult, AeroError>.Failure(AeroError.NotFound) =>
                Results.NotFound(new { message = "Manager identity authority is not configured." }),
            _ => Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Manager identity authority could not be loaded.")
        };
    }

    private static async Task<IResult> ConfigureAuthorityAsync(
        [FromBody] ConfigureManagerIdentityAuthorityRequest request,
        IManagerIdentityAuthorityService authorityService,
        CancellationToken cancellationToken)
    {
        var result = await authorityService.ConfigureAsync(request, cancellationToken);
        return MapAuthorityConfigurationResult(result);
    }

    private static async Task<IResult> ConfigureAuthorityFormAsync(
        [FromForm] ConfigureManagerIdentityAuthorityRequest request,
        IManagerIdentityAuthorityService authorityService,
        CancellationToken cancellationToken)
    {
        var result = await authorityService.ConfigureAsync(request, cancellationToken);
        return result is Result<ManagerIdentityAuthorityResult, AeroError>.Ok
            ? Results.LocalRedirect("/manager/authentication?configured=1")
            : Results.LocalRedirect("/manager/authentication?error=1");
    }

    private static IResult MapAuthorityConfigurationResult(
        Result<ManagerIdentityAuthorityResult, AeroError> result) => result switch
        {
            Result<ManagerIdentityAuthorityResult, AeroError>.Ok(var authority) => Results.Ok(authority),
            Result<ManagerIdentityAuthorityResult, AeroError>.Failure(AeroError.Validation) =>
                Results.BadRequest(new { message = "Manager identity authority request is invalid." }),
            Result<ManagerIdentityAuthorityResult, AeroError>.Failure(AeroError.Conflict) =>
                Results.Conflict(new { message = "Manager identity authority conflicts with the existing binding." }),
            Result<ManagerIdentityAuthorityResult, AeroError>.Failure(AeroError.NotFound) =>
                Results.NotFound(new { message = "Manager identity authority was not found." }),
            _ => Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Manager identity authority could not be configured.")
        };

    private static async Task<IResult> LogoutAsync(
        HttpContext httpContext,
        IManagerFederationCoordinator coordinator,
        CancellationToken cancellationToken)
    {
        var sessionClaims = httpContext.User.FindAll(ManagerFederationClaims.SessionId).ToArray();
        var providerClaims = httpContext.User.FindAll(ManagerFederationClaims.Provider).ToArray();
        var userIdClaims = httpContext.User.FindAll(ClaimTypes.NameIdentifier).ToArray();

        if (sessionClaims.Length == 1 && providerClaims.Length == 1 && userIdClaims.Length == 1 &&
            long.TryParse(sessionClaims[0].Value, NumberStyles.None, CultureInfo.InvariantCulture,
                out var sessionId) && sessionId > 0 &&
            string.Equals(sessionClaims[0].Value,
                sessionId.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal) &&
            long.TryParse(userIdClaims[0].Value, NumberStyles.None, CultureInfo.InvariantCulture,
                out var userId) && userId > 0 &&
            string.Equals(userIdClaims[0].Value,
                userId.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal) &&
            ManagerIdentityProviders.IsSupported(providerClaims[0].Value))
        {
            try
            {
                await coordinator.RevokeSessionAsync(sessionId, userId, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // Local cookie clearing proceeds even when durable session revocation is unavailable.
            }
        }

        await httpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        return Results.LocalRedirect(ReadSafeLogoutReturnPath(httpContext.Request.Query));
    }

    private static async Task<IResult> BeginLoginAsync(
        HttpContext httpContext,
        IManagerIdentityAuthorityService authorityService,
        IManagerFederationCoordinator coordinator,
        ManagerAuthenticationRateLimiter rateLimiter,
        CancellationToken cancellationToken)
    {
        var authorityResult = await authorityService.GetAsync(cancellationToken);
        if (authorityResult is not Result<ManagerIdentityAuthorityResult, AeroError>.Ok(var authority) ||
            !authority.IsActive || !authority.IsVerified ||
            !TryGetCallbackPath(authority.Provider, out var callbackPath) ||
            !RequestMatchesPublicOrigin(httpContext.Request, authority.PublicOrigin) ||
            !TryCreateCallbackUri(authority.PublicOrigin, callbackPath, out var callbackUri))
            return Failure();

        if (!rateLimiter.TryAcquireFederationBegin(httpContext))
            return Results.StatusCode(StatusCodes.Status429TooManyRequests);

        var returnPath = ReadSafeReturnPath(httpContext.Request.Query);
        var begin = await coordinator.BeginSignInAsync(
            new BeginManagerFederatedSignInRequest(callbackUri, returnPath), cancellationToken);
        return begin is Result<ManagerFederationBeginResult, AeroError>.Ok(var started) &&
               IsSafeProviderRedirect(started.Challenge.RedirectUri)
            ? Results.Redirect(started.Challenge.RedirectUri.AbsoluteUri)
            : Failure();
    }

    private static async Task<IResult> BeginLinkAsync(
        HttpContext httpContext,
        IRecoveryAdministratorAuthority recoveryAdministratorAuthority,
        IManagerIdentityAuthorityService authorityService,
        IManagerFederationCoordinator coordinator,
        CancellationToken cancellationToken)
    {
        var recoveryAdministratorId = await recoveryAdministratorAuthority.GetUserIdAsync(cancellationToken);
        var principalId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (recoveryAdministratorId is not > 0 ||
            !long.TryParse(principalId, NumberStyles.None, CultureInfo.InvariantCulture, out var currentUserId) ||
            currentUserId != recoveryAdministratorId.Value)
            return Failure();

        var authorityResult = await authorityService.GetAsync(cancellationToken);
        if (authorityResult is not Result<ManagerIdentityAuthorityResult, AeroError>.Ok(var authority) ||
            authority.IsActive || authority.IsVerified ||
            !TryGetCallbackPath(authority.Provider, out var callbackPath) ||
            !RequestMatchesPublicOrigin(httpContext.Request, authority.PublicOrigin) ||
            !TryCreateCallbackUri(authority.PublicOrigin, callbackPath, out var callbackUri))
            return Failure();

        var returnPath = ReadSafeReturnPath(httpContext.Request.Query);
        var begin = await coordinator.BeginRecoveryAdministratorLinkAsync(
            new BeginManagerFederationLinkRequest(recoveryAdministratorId.Value, callbackUri, returnPath),
            cancellationToken);
        return begin is Result<ManagerFederationBeginResult, AeroError>.Ok(var started) &&
               IsSafeProviderRedirect(started.Challenge.RedirectUri)
            ? Results.Redirect(started.Challenge.RedirectUri.AbsoluteUri)
            : Failure();
    }

    private static async Task<IResult> CompleteCallbackAsync(
        string expectedProvider,
        string callbackPath,
        HttpContext httpContext,
        IManagerIdentityAuthorityService authorityService,
        IManagerFederationCoordinator coordinator,
        IDocumentStore store,
        UserManager<AeroUser> userManager,
        SignInManager<AeroUser> signInManager,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var authorityResult = await authorityService.GetAsync(cancellationToken);
        if (authorityResult is not Result<ManagerIdentityAuthorityResult, AeroError>.Ok(var authority) ||
            !string.Equals(authority.Provider, expectedProvider, StringComparison.Ordinal) ||
            !RequestMatchesPublicOrigin(httpContext.Request, authority.PublicOrigin) ||
            !TryCreateCallbackUri(authority.PublicOrigin, callbackPath, out var callbackUri) ||
            !TryReadCallback(httpContext.Request.Query, out var state, out var code, out var error))
            return Failure();

        ManagerFederationCallbackResult completed;
        try
        {
            var result = await coordinator.CompleteCallbackAsync(expectedProvider,
                new CompleteManagerFederationCallbackRequest(callbackUri, state, code, error), cancellationToken);
            if (result is not Result<ManagerFederationCallbackResult, AeroError>.Ok(var value) ||
                !string.Equals(value.Provider, expectedProvider, StringComparison.Ordinal))
                return Failure();
            completed = value;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Failure();
        }

        try
        {
            var now = timeProvider.GetUtcNow();
            await using var readSession = await store.QuerySessionAsync(cancellationToken);
            var sessionRecord = await readSession.LoadAsync<ManagerFederatedSession>(
                completed.SessionId, cancellationToken);
            if (sessionRecord is null || sessionRecord.Id != completed.SessionId ||
                sessionRecord.UserId != completed.UserId || sessionRecord.AuthorityBindingId <= 0 ||
                sessionRecord.RevokedAt is not null || sessionRecord.ExpiresAt <= now ||
                sessionRecord.ExpiresAt != completed.ExpiresAt ||
                !string.Equals(sessionRecord.LoginProvider, completed.LoginProvider, StringComparison.Ordinal) ||
                !string.Equals(completed.LoginProvider,
                    $"AeroCms.ManagerFederation.{expectedProvider}", StringComparison.Ordinal) ||
                !ManagerFederationValidation.IsSafeReturnPath(completed.ReturnPath))
                return await CompensateAsync(completed, coordinator, httpContext);

            var binding = await readSession.LoadAsync<ManagerIdentityAuthorityBinding>(
                sessionRecord.AuthorityBindingId, cancellationToken);
            if (binding is null || binding.Id != sessionRecord.AuthorityBindingId ||
                !binding.IsActive || !binding.IsVerified ||
                !string.Equals(binding.Provider, expectedProvider, StringComparison.Ordinal) ||
                !ManagerIdentityAuthorityProjector.TryProject(binding, requireActive: true, out _))
                return await CompensateAsync(completed, coordinator, httpContext);

            var user = await userManager.FindByIdAsync(completed.UserId.ToString(CultureInfo.InvariantCulture));
            if (user is null || user.Id != completed.UserId || !user.IsActive || user.IsDeleted ||
                !(await userManager.GetRolesAsync(user)).Intersect(
                    CmsRoleNames.All, StringComparer.OrdinalIgnoreCase).Any())
                return await CompensateAsync(completed, coordinator, httpContext);

            var linkedUser = await userManager.FindByLoginAsync(completed.LoginProvider, completed.ProviderKey);
            if (linkedUser is null || linkedUser.Id != user.Id)
                return await CompensateAsync(completed, coordinator, httpContext);

            var principal = await signInManager.CreateUserPrincipalAsync(user);
            var identity = principal.Identities.FirstOrDefault(candidate => candidate.IsAuthenticated);
            if (identity is null)
                return await CompensateAsync(completed, coordinator, httpContext);

            foreach (var candidate in principal.Identities)
            {
                foreach (var claim in candidate.Claims.Where(claim =>
                             claim.Type is ManagerFederationClaims.SessionId or ManagerFederationClaims.Provider).ToArray())
                    candidate.RemoveClaim(claim);
            }
            identity.AddClaim(new Claim(ManagerFederationClaims.SessionId,
                completed.SessionId.ToString(CultureInfo.InvariantCulture)));
            identity.AddClaim(new Claim(ManagerFederationClaims.Provider, expectedProvider));

            await httpContext.SignInAsync(
                IdentityConstants.ApplicationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = false,
                    AllowRefresh = false,
                    ExpiresUtc = completed.ExpiresAt
                });
            return Results.LocalRedirect(completed.ReturnPath);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await CompensateAsync(completed, coordinator, httpContext);
            throw;
        }
        catch
        {
            return await CompensateAsync(completed, coordinator, httpContext);
        }
    }

    private static async Task<IResult> CompensateAsync(
        ManagerFederationCallbackResult completed,
        IManagerFederationCoordinator coordinator,
        HttpContext httpContext)
    {
        try
        {
            await coordinator.RevokeSessionAsync(completed.SessionId, completed.UserId, CancellationToken.None);
        }
        catch
        {
            // The callback remains failed even if durable compensation is unavailable.
        }

        try
        {
            await httpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        }
        catch
        {
            // No successful ordinary manager cookie is trusted after this callback.
        }

        return Failure();
    }

    private static bool TryReadCallback(
        IQueryCollection query,
        out string state,
        out string? code,
        out string? error)
    {
        state = string.Empty;
        code = null;
        error = null;
        if (query["state"].Count != 1 || query["code"].Count > 1 || query["error"].Count > 1)
            return false;

        state = query["state"][0] ?? string.Empty;
        code = query["code"].Count == 1 ? query["code"][0] : null;
        error = query["error"].Count == 1 ? query["error"][0] : null;
        return (code is not null) ^ (error is not null);
    }

    private static string ReadSafeReturnPath(IQueryCollection query)
    {
        if (query["returnUrl"].Count == 1)
        {
            var value = query["returnUrl"][0];
            if (ManagerFederationValidation.IsSafeReturnPath(value))
                return value!;
        }

        return "/manager";
    }

    private static string ReadSafeLogoutReturnPath(IQueryCollection query)
    {
        if (query["returnUrl"].Count == 1)
        {
            var value = query["returnUrl"][0];
            if (ManagerFederationValidation.IsSafeReturnPath(value))
                return value!;
        }

        return "/manager/login";
    }

    private static bool TryGetCallbackPath(string provider, out string callbackPath)
    {
        callbackPath = provider switch
        {
            ManagerIdentityProviders.EntraWorkforce => ManagerFederationRoutes.EntraWorkforceCallbackPath,
            ManagerIdentityProviders.WorkOs => ManagerFederationRoutes.WorkOsCallbackPath,
            _ => string.Empty
        };
        return callbackPath.Length > 0;
    }

    private static bool TryCreateCallbackUri(string publicOrigin, string callbackPath, out Uri callbackUri)
    {
        callbackUri = default!;
        if (!ManagerIdentityAuthorityRules.IsCanonicalPublicOrigin(publicOrigin) ||
            !Uri.TryCreate(publicOrigin, UriKind.Absolute, out var origin))
            return false;

        try
        {
            var candidate = new Uri(origin, callbackPath);
            if (!candidate.IsDefaultPort || !string.IsNullOrEmpty(candidate.UserInfo) ||
                !string.IsNullOrEmpty(candidate.Query) || !string.IsNullOrEmpty(candidate.Fragment))
                return false;
            callbackUri = candidate;
            return true;
        }
        catch (UriFormatException)
        {
            return false;
        }
    }

    private static bool RequestMatchesPublicOrigin(HttpRequest request, string publicOrigin)
    {
        if (!ManagerIdentityAuthorityRules.IsCanonicalPublicOrigin(publicOrigin) ||
            !Uri.TryCreate(publicOrigin, UriKind.Absolute, out var origin) ||
            !request.Host.HasValue || request.Host.Port is not null and not 443 ||
            !string.Equals(request.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
            return false;

        var requestHost = request.Host.Host;
        if (string.IsNullOrWhiteSpace(requestHost) || requestHost.Length > 253 ||
            requestHost.Any(character => char.IsControl(character) || char.IsWhiteSpace(character)) ||
            Uri.CheckHostName(requestHost) == UriHostNameType.Unknown)
            return false;

        try
        {
            var normalizedRequestHost = new UriBuilder(Uri.UriSchemeHttps, requestHost, -1).Uri.IdnHost;
            return string.Equals(normalizedRequestHost, origin.IdnHost, StringComparison.Ordinal);
        }
        catch (UriFormatException)
        {
            return false;
        }
    }

    private static bool IsSafeProviderRedirect(Uri? uri) =>
        uri is { IsAbsoluteUri: true, Scheme: "https", IsDefaultPort: true } &&
        string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Fragment);

    private static IResult Failure() => Results.LocalRedirect(FailurePath);
}
