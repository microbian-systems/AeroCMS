namespace Aero.Cms.Contracts.Models;

/// <summary>
/// Minimal identity snapshot serialized during prerendering via
/// <c>PersistentComponentState.PersistAsJson</c> and consumed on the WASM
/// side by <c>ServerAuthenticationStateProvider</c>.
///
/// This provides "instant auth" — the user is recognized immediately from
/// the prerendered HTML without an HTTP round-trip. The rich profile
/// (nickname, permissions) is fetched lazily from /auth/me.
///
/// Only includes data available from the server-side ClaimsPrincipal
/// (NameIdentifier, Name, Email, Role claims).
/// </summary>
public sealed record ClientAuthState(
    long UserId,
    string UserName,
    string? Email,
    IReadOnlyList<string> Roles);
