using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Contracts.Models;

namespace Aero.Cms.Web.Client.Services;

/// <summary>
/// Resolves Interactive WebAssembly authentication from prerendered state or the server Identity endpoint.
/// </summary>
/// <param name="httpClient">The same-origin client that sends the authentication cookie automatically.</param>
/// <param name="persistentState">The state transferred from server prerendering.</param>
/// <remarks>
/// Resolution first consumes the <c>ClientAuthState</c> prerender snapshot, then falls back to
/// <c>GET /api/v1/admin/auth/me</c>. The snapshot path supplies identity, email, and roles only;
/// nickname and permission claims are available only after the HTTP path. Any HTTP, JSON, or
/// unexpected exception fails closed to an unauthenticated principal. The snapshot infers the
/// <c>is_admin</c> claim from an <c>Admin</c> role, while the HTTP path trusts its explicit flag.
/// </remarks>
internal sealed class ServerAuthenticationStateProvider(
    HttpClient httpClient,
    PersistentComponentState persistentState)
    : AuthenticationStateProvider
{
    /// <summary>
    /// Gets the shared unauthenticated state returned by failed resolution.
    /// </summary>
    private static readonly AuthenticationState Unauthenticated =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    /// <summary>
    /// Caches the resolved principal until explicit invalidation.
    /// </summary>
    private AuthenticationState? _cachedAuthState;
    /// <summary>
    /// Caches the richer HTTP response when the fallback path succeeds.
    /// </summary>
    private CurrentUserResponse? _cachedUser;

    /// <summary>
    /// Gets the cached HTTP profile, or <see langword="null"/> when authentication came from
    /// prerendered state or has not resolved through HTTP.
    /// </summary>
    public CurrentUserResponse? CurrentUser => _cachedUser;

    /// <summary>
    /// Returns the cached state, consumes a prerender snapshot, or queries the current-user endpoint.
    /// </summary>
    /// <returns>The authenticated state, or an unauthenticated state on non-success, empty content, or exception.</returns>
public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        // Return cached result if we've already resolved
        if (_cachedAuthState is not null)
            return _cachedAuthState;

        // Phase 1: Try deserialized auth state from prerendering
        if (persistentState.TryTakeFromJson<ClientAuthState>(
            "ClientAuthState", out var snapshot) && snapshot is not null)
        {
            _cachedAuthState = BuildAuthStateFromSnapshot(snapshot);
            return _cachedAuthState;
        }

        // Phase 2: Fall back to HTTP call (current behavior)
        try
        {
            if (_cachedUser is null)
            {
                var response = await httpClient.GetAsync($"/{HttpConstants.ApiPrefix}admin/auth/me");
                if (!response.IsSuccessStatusCode)
                    return Cache(Unauthenticated);

                _cachedUser = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();
                if (_cachedUser is null)
                    return Cache(Unauthenticated);
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, _cachedUser.UserName),
                new("user_id", _cachedUser.UserId.ToString()),
            };

            if (_cachedUser.Email is not null)
                claims.Add(new Claim(ClaimTypes.Email, _cachedUser.Email));

            if (_cachedUser.Nickname is not null)
                claims.Add(new Claim("nickname", _cachedUser.Nickname));

            foreach (var role in _cachedUser.Roles)
                claims.Add(new Claim(ClaimTypes.Role, role));

            if (_cachedUser.IsAdmin)
                claims.Add(new Claim("is_admin", "true"));

            foreach (var perm in _cachedUser.Permissions)
                claims.Add(new Claim("permission", perm));

            var identity = new ClaimsIdentity(claims, "BlazorWebAppAuthentication");
            return Cache(new AuthenticationState(new ClaimsPrincipal(identity)));
        }
        catch
        {
            return Cache(Unauthenticated);
        }
    }

    /// <summary>
    /// Clears both caches and notifies subscribers with a new asynchronous resolution.
    /// </summary>
    /// <remarks>The notification is raised immediately with the unresolved task; this method does not await the HTTP refresh.</remarks>
public void InvalidateCache()
    {
        _cachedAuthState = null;
        _cachedUser = null;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    /// <summary>
    /// Stores and returns an authentication state.
    /// </summary>
    /// <param name="state">The resolved state.</param>
    /// <returns>The same instance.</returns>
    private AuthenticationState Cache(AuthenticationState state)
    {
        _cachedAuthState = state;
        return state;
    }

    /// <summary>
    /// Creates a principal from the limited prerender-safe authentication snapshot.
    /// </summary>
    /// <param name="snapshot">The server-persisted identity, email, and roles.</param>
    /// <returns>An authenticated state using the Blazor authentication type.</returns>
    private static AuthenticationState BuildAuthStateFromSnapshot(ClientAuthState snapshot)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, snapshot.UserName),
            new("user_id", snapshot.UserId.ToString()),
        };

        if (snapshot.Email is not null)
            claims.Add(new Claim(ClaimTypes.Email, snapshot.Email));

        foreach (var role in snapshot.Roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        if (snapshot.Roles.Contains("Admin", StringComparer.OrdinalIgnoreCase))
            claims.Add(new Claim("is_admin", "true"));

        return new AuthenticationState(
            new ClaimsPrincipal(new ClaimsIdentity(claims, "BlazorWebAppAuthentication")));
    }

    /// <summary>
    /// Models the richer response returned by the current-user Identity endpoint.
    /// </summary>
    /// <param name="UserId">The authenticated user's Snowflake identifier.</param>
    /// <param name="UserName">The login name.</param>
    /// <param name="Email">The optional email address.</param>
    /// <param name="Roles">The current Identity roles.</param>
    /// <param name="IsAdmin">Whether to emit the explicit <c>is_admin</c> claim.</param>
    /// <param name="Nickname">The optional nickname claim.</param>
    /// <param name="Permissions">Permission claim values supplied by the endpoint.</param>
public sealed record CurrentUserResponse(
        long UserId,
        string UserName,
        string? Email,
        IReadOnlyList<string> Roles,
        bool IsAdmin,
        string? Nickname,
        IReadOnlyList<string> Permissions);
}
