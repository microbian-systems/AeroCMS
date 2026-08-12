namespace Aero.Cms.Core.Infrastructure;

/// <summary>
/// Resolves a manager-selected site identifier to its server-owned tenant scope.
/// </summary>
/// <remarks>
/// The selected site identifier is request context only. Endpoint authorization must
/// establish that the caller may access it before this resolver is used.
/// </remarks>
public interface ISelectedSiteScopeResolver
{
    /// <summary>Loads the persisted tenant and site pair for a selected site.</summary>
    Task<SelectedSiteScope?> ResolveAsync(long selectedSiteId, CancellationToken cancellationToken = default);
}

/// <summary>A tenant/site pair loaded from server-side site storage.</summary>
public readonly record struct SelectedSiteScope(long TenantId, long SiteId)
{
    /// <summary>Gets whether both identifiers are valid.</summary>
    public bool IsValid => TenantId > 0 && SiteId > 0;
}
