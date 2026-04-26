using Aero.Core;
using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Abstractions.Http.Clients;

public sealed class AuthClient(
    HttpClient httpClient,
    ILogger<AuthClient> logger)
    : AeroCmsClientBase(httpClient, logger), IAuthClient
{
    public override string Path => "auth";

    public async Task<Result<JwtTokenResponse, AeroError>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        // Step 1: POST /api/v1/auth/login → AuthLoginResponse (user info + raw API key)
        var loginResult = await PostAsync<LoginRequest, AuthLoginResponse>(
            "/api/v1/auth/login", request, cancellationToken);

        // Step 2: Exchange the API key for a JWT via /api/v1/jwt/token
        return await loginResult.BindAsync(async authResponse =>
        {
            var tokenResult = await PostAsync<ApiKeyLoginRequest, JwtTokenResponse>(
                "/api/v1/jwt/token",
                new ApiKeyLoginRequest(authResponse.ApiKey),
                cancellationToken);

            return tokenResult;
        });
    }

    public async Task<Result<JwtTokenResponse, AeroError>> LoginWithApiKeyAsync(
        ApiKeyLoginRequest request,
        CancellationToken cancellationToken = default)
    {
        return await PostAsync<ApiKeyLoginRequest, JwtTokenResponse>(
            "/api/v1/jwt/token", request, cancellationToken);
    }

    public async Task<Result<JwtTokenResponse, AeroError>> RefreshAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        return await PostAsync<RefreshTokenRequest, JwtTokenResponse>(
            "/api/v1/jwt/refresh", request, cancellationToken);
    }
}
