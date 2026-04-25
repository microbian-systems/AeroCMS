namespace Aero.Cms.Core.Http.Clients;

public sealed class AuthClient(HttpClient httpClient) : IAuthClient
{
    public Task<JwtTokenResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<JwtTokenResponse> LoginWithApiKeyAsync(ApiKeyLoginRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<JwtTokenResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
