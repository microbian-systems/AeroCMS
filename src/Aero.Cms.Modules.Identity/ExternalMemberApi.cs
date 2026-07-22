using System.Text.Json;
using System.Text.Json.Serialization;
using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Core;
using Aero.Cms.Core.Entities;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using AeroDB.Sable;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;

namespace Aero.Cms.Modules.Identity;

/// <summary>Maps storefront-member authentication without exposing the manager cookie boundary.</summary>
public static class ExternalMemberApi
{
    private const string CallbackPath = "/api/v1/member/callback";
    private const string AccountPath = "/shop/account";
    private static readonly IReadOnlySet<string> BrowserLoginFields =
        new HashSet<string>(["invitationHandle"], StringComparer.Ordinal);

    /// <summary>Maps public sign-in and callback endpoints plus authenticated member endpoints.</summary>
    public static void MapExternalMemberApi(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup($"/{HttpConstants.ApiPrefix}member")
            .WithTags("Storefront - Member");

        group.MapPost("/login", BeginLoginAsync)
            .AllowAnonymous()
            .WithMetadata(new RequireAntiforgeryTokenAttribute());
        group.MapPost("/login/form", BeginLoginFormAsync)
            .AllowAnonymous()
            .WithMetadata(new RequireAntiforgeryTokenAttribute());
        group.MapGet("/callback", CompleteCallbackAsync)
            .AllowAnonymous();
        group.MapGet("/me", GetCurrentMember)
            .RequireAuthorization(ExternalMemberAuthenticationDefaults.Policy,
                ExternalMemberAuthenticationDefaults.SitePolicy);
        group.MapPost("/logout", LogoutAsync)
            .RequireAuthorization(ExternalMemberAuthenticationDefaults.Policy,
                ExternalMemberAuthenticationDefaults.SitePolicy)
            .WithMetadata(new RequireAntiforgeryTokenAttribute());
    }

    private static async Task<IResult> BeginLoginAsync(
        [FromBody] ExternalMemberLoginRequest request,
        [FromServices] IExternalMemberAuthenticationCoordinator coordinator,
        [FromServices] IAuthenticationSchemeProvider schemeProvider,
        [FromServices] ISiteContext siteContext,
        [FromServices] ExternalMemberProviderBeginRateLimiter rateLimiter,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (request.AdditionalProperties is { Count: > 0 } ||
            !TryCreateTrustedRoute(httpContext.Request, CallbackPath, out var route))
        {
            return LoginFailure();
        }
        if (!rateLimiter.TryAcquire(httpContext, siteContext, route.RequestHost))
            return Results.StatusCode(StatusCodes.Status429TooManyRequests);

        return await BeginLoginCoreAsync(
            request.InvitationHandle, request.ReturnPath, route, coordinator, schemeProvider, cancellationToken);
    }

    private static async Task<IResult> BeginLoginFormAsync(
        [FromServices] IExternalMemberAuthenticationCoordinator coordinator,
        [FromServices] IAuthenticationSchemeProvider schemeProvider,
        [FromServices] ISiteContext siteContext,
        [FromServices] ExternalMemberProviderBeginRateLimiter rateLimiter,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TryCreateTrustedRoute(httpContext.Request, CallbackPath, out var route) ||
            !httpContext.Request.HasFormContentType)
        {
            return LoginFailure();
        }
        if (!rateLimiter.TryAcquire(httpContext, siteContext, route.RequestHost))
            return Results.StatusCode(StatusCodes.Status429TooManyRequests);

        IFormCollection form;
        try
        {
            form = await httpContext.Request.ReadFormAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidDataException or BadHttpRequestException)
        {
            return LoginFailure();
        }

        if (form.Keys.Any(key => key != "__RequestVerificationToken" && !BrowserLoginFields.Contains(key)) ||
            form["invitationHandle"].Count > 1)
        {
            return LoginFailure();
        }

        var invitationHandle = form["invitationHandle"].Count == 1 &&
            !string.IsNullOrWhiteSpace(form["invitationHandle"][0])
                ? form["invitationHandle"][0]
                : null;
        return await BeginLoginCoreAsync(
            invitationHandle, AccountPath, route, coordinator, schemeProvider, cancellationToken);
    }

    private static async Task<IResult> BeginLoginCoreAsync(
        string? invitationHandle,
        string returnPath,
        ExternalMemberTrustedRoute route,
        IExternalMemberAuthenticationCoordinator coordinator,
        IAuthenticationSchemeProvider schemeProvider,
        CancellationToken cancellationToken)
    {
        var result = await coordinator.BeginAsync(
            new(invitationHandle, returnPath), route, cancellationToken);
        if (result is not Result<ExternalMemberAuthenticationBeginResult, AeroError>.Ok(var started))
            return LoginFailure();

        return await CreateChallengeResultAsync(started.Challenge, schemeProvider);
    }

    private static async Task<IResult> CompleteCallbackAsync(
        [FromServices] IExternalMemberAuthenticationCoordinator coordinator,
        [FromServices] ExternalMemberCookieIssuer cookieIssuer,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TryCreateTrustedRoute(httpContext.Request, CallbackPath, out var route) ||
            !TryReadSingleCallbackValues(httpContext.Request.Query, out var state, out var code, out var error))
        {
            return CallbackFailure();
        }

        var result = await coordinator.CallbackAsync(state, route, code, error, cancellationToken);
        if (result is not Result<ExternalMemberAuthenticationCallbackResult, AeroError>.Ok(var completed))
            return CallbackFailure();

        var receipt = completed.Receipt;
        return await cookieIssuer.TryIssueAsync(httpContext, receipt)
            ? Results.LocalRedirect(receipt.ReturnPath)
            : Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "External sign-in could not be completed.");
    }

    private static IResult GetCurrentMember([FromServices] ICurrentPrincipal currentPrincipal)
    {
        if (!currentPrincipal.IsAuthenticated || currentPrincipal.Kind != PrincipalKind.ExternalMember ||
            currentPrincipal.PrincipalId is not long memberId ||
            currentPrincipal.ExternalSessionId is not long sessionId ||
            currentPrincipal.SecurityVersion is not long securityVersion ||
            string.IsNullOrWhiteSpace(currentPrincipal.AuthenticationProvider))
        {
            return Results.Unauthorized();
        }

        return Results.Ok(new CurrentExternalMemberResponse(
            memberId, currentPrincipal.AuthenticationProvider, sessionId, securityVersion));
    }

    private static async Task<IResult> LogoutAsync(
        [FromServices] ICurrentPrincipal currentPrincipal,
        [FromServices] IExternalMemberSessionRevocationService revocationService,
        [FromServices] IQuerySession querySession,
        [FromServices] IExternalMemberProviderStrategyFactory strategyFactory,
        [FromServices] IExternalProviderSecretSource secretSource,
        [FromServices] ISiteContext siteContext,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        IResult result;
        try
        {
            if (!TryCreateLogoutRequest(currentPrincipal, siteContext, out var request))
            {
                result = Results.Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "The local member session could not be revoked.");
                return await ClearMemberCookieAsync(httpContext, result);
            }

            var revoked = await revocationService.RevokeAsync(request, cancellationToken);
            if (revoked is not Result<ExternalMemberSessionRevocationReceipt, AeroError>.Ok(var receipt))
            {
                result = Results.Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "The local member session could not be revoked.");
                return await ClearMemberCookieAsync(httpContext, result);
            }

            if (!await TryClearMemberCookieAsync(httpContext))
                return Results.Problem(statusCode: StatusCodes.Status500InternalServerError,
                    title: "The browser member session could not be cleared.");

            result = Results.NoContent();
            var providerSupportsLogout =
                receipt.Provider == ExternalMemberProviders.EntraExternalId ||
                receipt.Provider == ExternalMemberProviders.WorkOs &&
                !string.IsNullOrWhiteSpace(receipt.ProviderSessionReference);
            if (providerSupportsLogout &&
                siteContext.TenantId == receipt.TenantId && siteContext.SiteId == receipt.SiteId &&
                TryCreateSameSiteRoot(httpContext.Request, out var returnUri))
            {
                var redirect = await TryPrepareUpstreamLogoutAsync(
                    receipt, returnUri, querySession, strategyFactory, secretSource, cancellationToken);
                if (redirect is not null)
                    result = redirect;
            }

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await ClearMemberCookieAsync(httpContext, Results.NoContent());
            throw;
        }
        catch
        {
            result = Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "The local member session could not be revoked.");
            return await ClearMemberCookieAsync(httpContext, result);
        }
    }

    private static async Task<IResult> ClearMemberCookieAsync(HttpContext context, IResult result)
    {
        return await TryClearMemberCookieAsync(context) ? result : Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "The browser member session could not be cleared.");
    }

    private static async Task<bool> TryClearMemberCookieAsync(HttpContext context)
    {
        try { await context.SignOutAsync(ExternalMemberAuthenticationDefaults.Scheme); return true; }
        catch { return false; }
    }

    private static async Task<IResult?> TryPrepareUpstreamLogoutAsync(
        ExternalMemberSessionRevocationReceipt receipt,
        Uri returnUri,
        IQuerySession querySession,
        IExternalMemberProviderStrategyFactory strategyFactory,
        IExternalProviderSecretSource secretSource,
        CancellationToken cancellationToken)
    {
        try
        {
            var bindings = await querySession.Query<ExternalOrganizationBinding>()
                .Where(binding => binding.TenantId == receipt.TenantId && binding.IsActive)
                .ToListAsync(cancellationToken);
            if (bindings.Count != 1 ||
                !ExternalProviderAuthorityProjector.TryProject(bindings[0], receipt.TenantId, out var authority) ||
                !string.Equals(authority.Provider, receipt.Provider, StringComparison.Ordinal) ||
                strategyFactory.Resolve(receipt.Provider) is not Result<IExternalMemberProviderStrategy, AeroError>.Ok(var strategy))
            {
                return null;
            }

            var credentials = await secretSource.ReadAsync(authority.SecretReference, cancellationToken);
            if (credentials is not Result<ExternalProviderCredentialBundle, AeroError>.Ok(var bundle))
                return null;

            using (bundle)
            {
                var prepared = await strategy.PrepareLogoutAsync(new(
                    authority,
                    receipt.SiteId,
                    returnUri,
                    receipt.ProviderSessionReference), bundle, cancellationToken);
                return prepared is Result<ExternalProviderAuthorizationChallenge, AeroError>.Ok(var challenge) &&
                    TryValidateUpstreamLogout(authority, receipt, returnUri, challenge, out var target)
                        ? Results.Redirect(target.AbsoluteUri)
                        : null;
            }
        }
        catch
        {
            return null;
        }
    }

    private static bool TryValidateUpstreamLogout(
        ExternalProviderAuthority authority,
        ExternalMemberSessionRevocationReceipt receipt,
        Uri returnUri,
        ExternalProviderAuthorizationChallenge challenge,
        out Uri target)
    {
        target = default!;
        if (challenge.Kind != ExternalProviderAuthorizationChallengeKind.Redirect ||
            !TryValidateAbsoluteHttpsRedirect(challenge.Target, out var candidate))
        {
            return false;
        }

        var query = QueryHelpers.ParseQuery(candidate.Query);
        if (authority.Provider == ExternalMemberProviders.WorkOs)
        {
            if (receipt.ProviderSessionReference is not { Length: > 0 } sid || query.Count != 2 ||
                candidate.Host != "api.workos.com" ||
                candidate.AbsolutePath != "/user_management/sessions/logout" ||
                query["session_id"].Count != 1 || query["session_id"][0] != sid ||
                query["return_to"].Count != 1 || query["return_to"][0] != returnUri.AbsoluteUri)
                return false;
        }
        else if (authority.Provider == ExternalMemberProviders.EntraExternalId)
        {
            var authorityUri = new Uri(authority.Authority);
            var suffix = $"/{authority.OrganizationId}/v2.0";
            var expectedPath = authorityUri.AbsolutePath[..^suffix.Length] +
                $"/{authority.OrganizationId}/oauth2/v2.0/logout";
            if (query.Count != 1 || candidate.Host != authorityUri.Host ||
                candidate.AbsolutePath != expectedPath ||
                query["post_logout_redirect_uri"].Count != 1 ||
                query["post_logout_redirect_uri"][0] != returnUri.AbsoluteUri)
                return false;
        }
        else
        {
            return false;
        }

        target = candidate;
        return true;
    }

    private static async Task<IResult> CreateChallengeResultAsync(
        ExternalProviderAuthorizationChallenge challenge,
        IAuthenticationSchemeProvider schemeProvider)
    {
        if (challenge.Kind == ExternalProviderAuthorizationChallengeKind.Redirect &&
            challenge.Parameters.Count == 0 &&
            TryValidateAbsoluteHttpsRedirect(challenge.Target, out var redirect))
        {
            return Results.Redirect(redirect.AbsoluteUri);
        }

        if (challenge.Kind != ExternalProviderAuthorizationChallengeKind.NamedScheme ||
            !IsSafeSchemeName(challenge.Target) ||
            challenge.Parameters.Any(parameter => !IsSafeAuthenticationParameter(parameter)))
        {
            return LoginFailure();
        }

        var scheme = await schemeProvider.GetSchemeAsync(challenge.Target);
        if (scheme is null ||
            string.Equals(scheme.Name, ExternalMemberAuthenticationDefaults.Scheme, StringComparison.Ordinal) ||
            scheme.Name.StartsWith("Identity.", StringComparison.Ordinal))
        {
            return LoginFailure();
        }

        var properties = new AuthenticationProperties
        {
            IsPersistent = false,
            AllowRefresh = false
        };
        foreach (var parameter in challenge.Parameters)
            properties.Items.Add(parameter.Key, parameter.Value);
        return Results.Challenge(properties, [scheme.Name]);
    }

    private static bool IsSafeAuthenticationParameter(KeyValuePair<string, string> parameter) =>
        parameter.Key switch
        {
            "aerocms:authentication_handle" => ExternalMemberIssuanceRules.IsOpaqueHandle(parameter.Value),
            "aerocms:return_path" => ExternalMemberIssuanceRules.IsSafeLocalReturnPath(parameter.Value),
            _ => false
        };

    private static bool IsSafeSchemeName(string? value) =>
        value is { Length: > 0 and <= 128 } &&
        value.All(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_');

    private static bool TryReadSingleCallbackValues(
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
        return ExternalMemberIssuanceRules.IsOpaqueHandle(state) &&
            ((code is not null) ^ (error is not null));
    }

    private static bool TryCreateLogoutRequest(
        ICurrentPrincipal principal,
        ISiteContext siteContext,
        out ExternalMemberSessionRevocationRequest request)
    {
        request = default!;
        if (!principal.IsAuthenticated || principal.Kind != PrincipalKind.ExternalMember ||
            principal.PrincipalId is not long memberId ||
            principal.ExternalSessionId is not long sessionId ||
            principal.SecurityVersion is not long version ||
            siteContext.TenantId <= 0 || siteContext.SiteId <= 0 ||
            string.IsNullOrWhiteSpace(principal.AuthenticationProvider))
        {
            return false;
        }

        request = new(siteContext.TenantId, siteContext.SiteId, memberId, sessionId,
            principal.AuthenticationProvider, version);
        return true;
    }

    private static bool TryCreateTrustedRoute(
        HttpRequest request,
        string path,
        out ExternalMemberTrustedRoute route)
    {
        route = default!;
        if (!TryCreateOrigin(request, path, out var callback))
            return false;
        route = new(callback, callback.Host);
        return true;
    }

    private static bool TryCreateSameSiteRoot(HttpRequest request, out Uri uri) =>
        TryCreateOrigin(request, "/", out uri);

    private static bool TryCreateOrigin(HttpRequest request, string path, out Uri uri)
    {
        uri = default!;
        try
        {
            if (!string.Equals(request.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal) ||
                !request.Host.HasValue || request.Host.Port is not null and not 443)
            {
                return false;
            }

            var rawHost = request.Host.Host;
            if (string.IsNullOrWhiteSpace(rawHost) || rawHost.Length > 253 ||
                !string.Equals(rawHost, rawHost.ToLowerInvariant(), StringComparison.Ordinal) ||
                rawHost.EndsWith(".", StringComparison.Ordinal) ||
                rawHost.Any(character => char.IsControl(character) || char.IsWhiteSpace(character)) ||
                Uri.CheckHostName(rawHost) == UriHostNameType.Unknown)
            {
                return false;
            }

            if (!Uri.TryCreate($"https://{request.Host}{path}", UriKind.Absolute, out var candidate) ||
                !candidate.IsDefaultPort || !string.IsNullOrEmpty(candidate.UserInfo) ||
                !string.IsNullOrEmpty(candidate.Query) || !string.IsNullOrEmpty(candidate.Fragment) ||
                !string.Equals(candidate.Host, rawHost, StringComparison.Ordinal))
            {
                return false;
            }

            uri = candidate;
            return true;
        }
        catch (Exception exception) when (exception is UriFormatException or InvalidOperationException)
        {
            return false;
        }
    }

    private static bool TryValidateAbsoluteHttpsRedirect(string? value, out Uri uri)
    {
        uri = default!;
        if (value is not { Length: > 0 and <= 4096 } ||
            !string.Equals(value, value.Trim(), StringComparison.Ordinal) ||
            !Uri.TryCreate(value, UriKind.Absolute, out var candidate) ||
            !string.Equals(candidate.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal) ||
            !candidate.IsDefaultPort || string.IsNullOrWhiteSpace(candidate.Host) ||
            !string.IsNullOrEmpty(candidate.UserInfo) || !string.IsNullOrEmpty(candidate.Fragment) ||
            !string.Equals(value, candidate.AbsoluteUri, StringComparison.Ordinal))
        {
            return false;
        }

        uri = candidate;
        return true;
    }

    private static IResult LoginFailure() => Results.BadRequest(new
    {
        message = "External sign-in could not be started."
    });

    private static IResult CallbackFailure() => Results.BadRequest(new
    {
        message = "External sign-in could not be completed."
    });

    /// <summary>Accepts only an invitation handle and a local return path.</summary>
    public sealed record ExternalMemberLoginRequest(
        string? InvitationHandle,
        string ReturnPath)
    {
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? AdditionalProperties { get; init; }
    }

    /// <summary>Describes the local external-member session visible to storefront code.</summary>
    public sealed record CurrentExternalMemberResponse(
        long MemberId,
        string AuthenticationProvider,
        long SessionId,
        long SecurityVersion);
}
