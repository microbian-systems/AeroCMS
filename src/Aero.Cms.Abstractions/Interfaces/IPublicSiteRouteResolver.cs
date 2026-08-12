namespace Aero.Cms.Abstractions.Interfaces;

/// <summary>
/// Resolves the minimum enabled-site scope required to select a public, host-routed endpoint.
/// </summary>
/// <remarks>
/// Endpoint selection occurs before the normal site-resolution middleware. Implementations must
/// derive this scope from the request host and must never trust route values or client cookies.
/// </remarks>
public interface IPublicSiteRouteResolver
{
    /// <summary>Resolves an enabled public site by host, or returns <see langword="null"/> when no enabled site owns it.</summary>
    Task<PublicSiteRouteScope?> ResolveAsync(string host, CancellationToken cancellationToken = default);
}

/// <summary>The enabled site scope used exclusively for public endpoint selection.</summary>
public sealed record PublicSiteRouteScope(long SiteId, string DefaultCulture, IReadOnlyList<string> SupportedCultures);
