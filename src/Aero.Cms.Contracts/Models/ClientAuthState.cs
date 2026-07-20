namespace Aero.Cms.Contracts.Models;

/// <summary>
/// Minimal identity snapshot serialized during prerendering via
/// <c>PersistentComponentState.PersistAsJson</c> and consumed on the WASM
/// side by <c>ServerAuthenticationStateProvider</c>.
///
/// The record carries identifier, name, email, and role values projected from the
/// server-side claims principal. It contains no authorization behavior.
/// </summary>
/// <param name="UserId">The authenticated user's identifier.</param>
/// <param name="UserName">The authenticated user's name claim.</param>
/// <param name="Email">The authenticated user's email claim, if available.</param>
/// <param name="Roles">The authenticated user's role claims.</param>
public sealed record ClientAuthState(
    long UserId,
    string UserName,
    string? Email,
    IReadOnlyList<string> Roles);
