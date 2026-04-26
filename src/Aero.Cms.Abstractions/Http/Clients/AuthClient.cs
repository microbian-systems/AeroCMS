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
        var test = this.client.BaseAddress;
        log.LogDebug("base url is {u}", test);
        // Step 1: POST /api/v1/auth/login → AuthLoginResponse (user info + raw API key)
        var loginResult = await PostAsync<LoginRequest, AuthLoginResponse>(
            $"{HttpConstants.ApiPrefix}{Path}/login", request, cancellationToken);

        // Step 2: Exchange the API key for a JWT via /api/v1/jwt/token
        var result = await loginResult.BindAsync(async authResponse =>
        {
            var tokenResult = await PostAsync<ApiKeyLoginRequest, JwtTokenResponse>(
                $"{HttpConstants.ApiPrefix}jwt/token",
                new ApiKeyLoginRequest(authResponse.ApiKey),
                cancellationToken);

            return tokenResult;
        });

        return result;
    }

    public async Task<Result<JwtTokenResponse, AeroError>> LoginWithApiKeyAsync(
        ApiKeyLoginRequest request,
        CancellationToken cancellationToken = default)
    {
        return await PostAsync<ApiKeyLoginRequest, JwtTokenResponse>(
            $"{HttpConstants.ApiPrefix}jwt/token", request, cancellationToken);
    }

    public async Task<Result<JwtTokenResponse, AeroError>> RefreshAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        return await PostAsync<RefreshTokenRequest, JwtTokenResponse>(
            $"{HttpConstants.ApiPrefix}jwt/refresh", request, cancellationToken);
    }
}
