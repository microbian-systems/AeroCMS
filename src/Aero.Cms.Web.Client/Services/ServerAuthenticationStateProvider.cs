using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace Aero.Cms.Web.Client.Services;

/// <summary>
/// AuthenticationStateProvider for InteractiveWebAssembly rendering.
///
/// Since Blazor WASM can't directly access cookies or HttpContext, this provider
/// calls the Identity API's /me endpoint to determine the current user's auth state.
/// The browser automatically sends the .AeroCms.Auth cookie with this request,
/// so the server can identify the user.
///
/// This replaces AddAuthenticationStateDeserialization() which isn't available as
/// an IServiceCollection extension in the current Microsoft.AspNetCore.Components.WebAssembly
/// package.
/// </summary>
internal sealed class ServerAuthenticationStateProvider(
    HttpClient httpClient)
    : AuthenticationStateProvider
{
    private static readonly AuthenticationState Unauthenticated =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    private CurrentUserResponse? _cachedUser;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            if (_cachedUser is null)
            {
                var response = await httpClient.GetAsync("/api/v1/admin/auth/me");
                if (!response.IsSuccessStatusCode)
                    return Unauthenticated;

                _cachedUser = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();
                if (_cachedUser is null)
                    return Unauthenticated;
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, _cachedUser.UserName),
            };

            if (_cachedUser.Email is not null)
                claims.Add(new Claim(ClaimTypes.Email, _cachedUser.Email));

            foreach (var role in _cachedUser.Roles)
                claims.Add(new Claim(ClaimTypes.Role, role));

            var identity = new ClaimsIdentity(claims, "BlazorWebAppAuthentication");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
        catch
        {
            return Unauthenticated;
        }
    }

    public void InvalidateCache()
    {
        _cachedUser = null;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    private sealed record CurrentUserResponse(
        string UserName,
        string? Email,
        IReadOnlyList<string> Roles);
}
