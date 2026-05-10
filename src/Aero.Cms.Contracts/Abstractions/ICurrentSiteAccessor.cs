using Aero.Cms.Contracts.Models;

namespace Aero.Cms.Contracts.Abstractions;

/// <summary>
/// Manages the currently selected site context for the admin manager.
/// Uses a cookie ("AeroCms.SiteId") for persistence across requests.
/// WASM-safe — no Orleans dependencies.
/// </summary>
public interface ICurrentSiteAccessor
{
    /// <summary>Raised when the current site changes.</summary>
    event Action? SiteChanged;

    /// <summary>Returns the current site info, or null if no site is selected.</summary>
    Task<SiteInfo?> GetCurrentSiteAsync();

    /// <summary>Returns the current site ID, or null if no site is selected.</summary>
    Task<long?> GetCurrentSiteIdAsync();

    /// <summary>Sets the current site by ID. Persists to cookie.</summary>
    Task SetCurrentSiteAsync(long siteId);

    /// <summary>Clears the current site selection.</summary>
    Task ClearCurrentSiteAsync();
}
