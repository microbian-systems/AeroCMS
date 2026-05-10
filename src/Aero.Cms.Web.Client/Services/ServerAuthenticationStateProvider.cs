using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Contracts.Models;

namespace Aero.Cms.Web.Client.Services;

/// <summary>
/// Hybrid AuthenticationStateProvider for InteractiveWebAssembly rendering.
///
/// Two-phase auth resolution:
///   1. Instant: reads a <see cref="ClientAuthState"/> snapshot from
///      <see cref="PersistentComponentState"/> (serialized during server prerendering).
///      This provides authentication with zero network latency.
///   2. Fallback: calls <c>GET /api/v1/admin/auth/me</c> to determine the user's
///      auth state via the .AeroCms.Auth cookie.
///
/// The rich profile (nickname, permissions) is fetched separately by the
/// manager shell layout via AppState.LoadPermissions() and cached there.
/// </summary>
internal sealed class ServerAuthenticationStateProvider(
    HttpClient httpClient,
    PersistentComponentState persistentState)
    : AuthenticationStateProvider
{
    private static readonly AuthenticationState Unauthenticated =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    private AuthenticationState? _cachedAuthState;
    private CurrentUserResponse? _cachedUser;

    /// <summary>
    /// Returns the cached current user response from HTTP call, or null
    /// if auth was resolved from prerendered snapshot (no HTTP call made).
    /// Components can read this after auth resolution for rich profile data.
    /// </summary>
    public CurrentUserResponse? CurrentUser => _cachedUser;

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

    public void InvalidateCache()
    {
        _cachedAuthState = null;
        _cachedUser = null;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    private AuthenticationState Cache(AuthenticationState state)
    {
        _cachedAuthState = state;
        return state;
    }

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

    public sealed record CurrentUserResponse(
        long UserId,
        string UserName,
        string? Email,
        IReadOnlyList<string> Roles,
        bool IsAdmin,
        string? Nickname,
        IReadOnlyList<string> Permissions);
}
