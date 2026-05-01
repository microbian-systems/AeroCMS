using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Abstractions.Http.Clients;

public sealed record LoginRequest(
    string UserName,
    string Password,
    bool RememberMe = false);

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

    /// <summary>
    /// Logs in via the ASP.NET Core Identity cookie endpoint.
    /// On success, the server sets the .AeroCms.Auth cookie in the HTTP response.
    /// In InteractiveWebAssembly mode, the browser's fetch API stores this cookie
    /// automatically, making it available on subsequent full-page navigations.
    /// </summary>
    Task<HttpResponseMessage> LoginWithCookieAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);
}
