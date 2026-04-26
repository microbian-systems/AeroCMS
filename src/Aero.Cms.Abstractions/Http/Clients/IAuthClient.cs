using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Abstractions.Http.Clients;

public sealed record LoginRequest(
    string UserName,
    string Password);

public sealed record ApiKeyAuthRequest(
    string ApiKey);

public sealed record RefreshTokenRequest(
    string RefreshToken);

public sealed record JwtTokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt);

public sealed record AuthLoginResponse(
    long UserId,
    string UserName,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles,
    string ApiKey);

public interface IAuthClient
{
    Task<Result<JwtTokenResponse, AeroError>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<JwtTokenResponse, AeroError>> AuthWithApiKeyAsync(
        ApiKeyAuthRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<JwtTokenResponse, AeroError>> RefreshJwtTokenAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default);
}
