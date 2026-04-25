namespace Aero.Cms.Core.Http.Clients;

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

public interface IAuthClient
{
    Task<JwtTokenResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);

    Task<JwtTokenResponse> LoginWithApiKeyAsync(
        ApiKeyLoginRequest request,
        CancellationToken cancellationToken = default);

    Task<JwtTokenResponse> RefreshAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default);
}
