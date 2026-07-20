using Aero.Auth.Services;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Services;
using Aero.Models.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Jwt.Areas.Api.v1;

/// <summary>
/// Carries a refresh token submitted to the headless refresh endpoint.
/// </summary>
/// <param name="RefreshToken">
/// The raw token forwarded to the configured <c>IRefreshTokenService</c>.
/// This record does not validate that the value is present or well formed.
/// </param>
public sealed record HeadlessRefreshTokenRequest(string RefreshToken);
/// <summary>
/// Returns an access token, a refresh token, and the endpoint's estimated
/// access-token expiration time.
/// </summary>
/// <param name="AccessToken">The access token returned by the configured token service.</param>
/// <param name="RefreshToken">The raw refresh token returned by the configured refresh-token service.</param>
/// <param name="ExpiresAt">
/// The expiration estimate calculated by the endpoint rather than parsed from
/// <paramref name="AccessToken"/>.
/// </param>
/// <remarks>
/// Both token values are bearer credentials and are returned without redaction.
/// When the configured service is not the concrete <c>JwtTokenService</c>, the
/// expiration estimate uses a 300-second fallback.
/// </remarks>
public sealed record HeadlessJwtResponse(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);

/// <summary>
/// Maps the headless API-key-to-token and refresh-token HTTP endpoints.
/// </summary>
/// <remarks>
/// Token generation, signing, refresh-token persistence, and rotation are
/// delegated to services resolved from the host. This type does not establish
/// key secrecy, rotation atomicity, old-token invalidation, cross-node
/// persistence, tenant scope, or access-token revocation. The default refresh
/// model's active-state predicate ignores its replacement link, so rotation
/// does not make an otherwise unexpired, unrevoked old token inactive.
/// </remarks>
public static class JwtApi
{
        /// <summary>
    /// Maps <c>POST /api/v1/jwt/token</c> and
    /// <c>POST /api/v1/jwt/refresh</c>.
    /// </summary>
    /// <param name="app">The endpoint route builder to update.</param>
    /// <remarks>
    /// The token endpoint delegates API-key authentication, requests an access
    /// token using the returned user's identifier, email, and roles, and creates
    /// a refresh token labeled for the <c>headless</c> client. The refresh
    /// endpoint first asks the refresh-token service to rotate and commit the
    /// supplied credential, then validates the returned token and requires the
    /// associated user to exist, be active, and not be deleted before requesting
    /// another access token. Because rotation commits first, any later 401 or
    /// 500 can leave the token-store mutation committed without returning the
    /// replacement credential to the caller.
    ///
    /// The token handler returns 200 on success, 401 when API-key authentication
    /// returns no user, and 500 for caught exceptions. The refresh handler
    /// returns 200 on success, 401 when the newly returned credential does not
    /// validate or its user is missing/inactive/deleted, and 500 for caught
    /// exceptions. With the default service, an invalid, expired, or revoked old
    /// refresh token can throw during rotation and therefore become a 500 rather
    /// than a 401. Both 500 problem responses expose
    /// <see cref="Exception.Message"/>.
    ///
    /// Neither JSON POST endpoint attaches an explicit authorization policy,
    /// antiforgery metadata, rate limit, or tenant constraint. Cancellation is
    /// forwarded to authentication, token, and refresh-token services, but not
    /// to the Identity user and role lookups, whose APIs are called without this
    /// request token.
    ///
    /// The default refresh-token implementation records replacement links but
    /// <c>RefreshToken.IsActive</c> checks only revocation and expiration.
    /// Consequently, a rotated old token remains reusable until it expires or
    /// is explicitly revoked; this endpoint does not provide one-time-use
    /// refresh semantics.
    /// </remarks>
public static void MapJwtApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{HttpConstants.ApiPrefix}jwt")
            .WithTags("Headless - Bearer Auth");

        group.MapPost("/token", CreateToken)
            .WithName("CreateHeadlessToken");

        group.MapPost("/refresh", RefreshToken)
            .WithName("RefreshHeadlessToken");
    }

    private static async Task<IResult> CreateToken(
        [FromBody] ApiKeyAuthRequest request,
        IAuthenticationService authService,
        [FromServices] IJwtTokenService jwtService,
        [FromServices] IRefreshTokenService refreshTokenService,
        UserManager<AeroUser> userManager,
        IHttpContextAccessor httpContextAccessor,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(typeof(JwtApi));
        try
        {
            var user = await authService.AuthenticateAsync(request, cancellationToken);
            if (user == null)
            {
                return TypedResults.Unauthorized();
            }

            var roles = await userManager.GetRolesAsync(user);
            var accessToken = await jwtService.GenerateAccessTokenAsync(user.Id, user.Email!, roles, cancellationToken: cancellationToken);
            
            var context = httpContextAccessor.HttpContext;
            var ipAddress = context?.Connection.RemoteIpAddress?.ToString();
            var userAgent = context?.Request.Headers.UserAgent.ToString();

            var refreshToken = await refreshTokenService.GenerateRefreshTokenAsync(
                user.Id, 
                "headless", 
                ipAddress, 
                userAgent, 
                cancellationToken);

            // Access token lifetime is handled by JwtTokenService, we estimate ExpiresAt for the response
            var expiresAt = DateTimeOffset.UtcNow.AddSeconds(jwtService is JwtTokenService jts ? jts.AccessTokenLifetime : 300);

            return TypedResults.Ok(new HeadlessJwtResponse(
                accessToken,
                refreshToken,
                expiresAt));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating headless token");
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> RefreshToken(
        HeadlessRefreshTokenRequest request,
        [FromServices] IJwtTokenService jwtService,
        [FromServices] IRefreshTokenService refreshTokenService,
        UserManager<AeroUser> userManager,
        IHttpContextAccessor httpContextAccessor,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(typeof(JwtApi));
        try
        {
            var context = httpContextAccessor.HttpContext;
            var ipAddress = context?.Connection.RemoteIpAddress?.ToString();
            var userAgent = context?.Request.Headers.UserAgent.ToString();

            // The default service commits the rotation before this handler performs its later checks.
            var newToken = await refreshTokenService.RotateRefreshTokenAsync(
                request.RefreshToken,
                "headless",
                ipAddress,
                userAgent,
                cancellationToken);

            // Get user ID from the new token
            var userId = await refreshTokenService.ValidateRefreshTokenAsync(newToken, cancellationToken);
            if (userId == null)
            {
                return TypedResults.Unauthorized();
            }

            var user = await userManager.FindByIdAsync(userId.Value.ToString());
            if (user == null || !user.IsActive || user.IsDeleted)
            {
                return TypedResults.Unauthorized();
            }

            var roles = await userManager.GetRolesAsync(user);
            var accessToken = await jwtService.GenerateAccessTokenAsync(user.Id, user.Email!, roles, cancellationToken: cancellationToken);
            
            var expiresAt = DateTimeOffset.UtcNow.AddSeconds(jwtService is JwtTokenService jts ? jts.AccessTokenLifetime : 300);

            return TypedResults.Ok(new HeadlessJwtResponse(
                accessToken,
                newToken,
                expiresAt));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error refreshing headless token");
            return TypedResults.Problem(ex.Message);
        }
    }
}
