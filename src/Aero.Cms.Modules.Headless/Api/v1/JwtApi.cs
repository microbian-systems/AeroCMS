using Aero.Auth.Services;
using Aero.Cms.Abstractions.Services;
using Aero.Models;
using Aero.Models.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Headless.Api.v1;

public sealed record HeadlessRefreshTokenRequest(string RefreshToken);
public sealed record HeadlessJwtResponse(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);

public static class JwtApi
{
    public static void MapJwtApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/jwt")
            .WithTags("Headless - Bearer Auth");

        group.MapPost("/token", CreateToken)
            .WithName("CreateHeadlessToken");

        group.MapPost("/refresh", RefreshToken)
            .WithName("RefreshHeadlessToken");
    }

    private static async Task<IResult> CreateToken(
        [FromBody] ApiKeyAuthRequestModel request,
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

            // Rotate the refresh token
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
