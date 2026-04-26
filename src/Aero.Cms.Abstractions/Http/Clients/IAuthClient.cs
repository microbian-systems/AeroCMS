using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Abstractions.Http.Clients;

public sealed record LoginRequest(
    string UserName,
    string Password);

public sealed record ApiKeyLoginRequest(
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

    Task<Result<JwtTokenResponse, AeroError>> LoginWithApiKeyAsync(
        ApiKeyLoginRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<JwtTokenResponse, AeroError>> RefreshAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default);
}
