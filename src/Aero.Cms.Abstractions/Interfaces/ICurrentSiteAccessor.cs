namespace Aero.Cms.Abstractions.Interfaces;

/// <summary>
/// Manages the currently selected site context for the admin manager.
/// Uses a cookie ("AeroCms.SiteId") for persistence across requests and circuits.
/// </summary>
public interface ICurrentSiteAccessor
{
    /// <summary>Raised when the current site changes.</summary>
    event Action? SiteChanged;

    /// <summary>Returns the current site view model, or null if no site is selected.</summary>
    Task<SiteViewModel?> GetCurrentSiteAsync();

    /// <summary>Returns the current site ID, or null if no site is selected.</summary>
    Task<long?> GetCurrentSiteIdAsync();

    /// <summary>Sets the current site by ID. Persists to cookie.</summary>
    Task SetCurrentSiteAsync(long siteId);

    /// <summary>Clears the current site selection.</summary>
    Task ClearCurrentSiteAsync();
}
