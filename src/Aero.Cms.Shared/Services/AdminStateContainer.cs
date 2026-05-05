using Aero.Cms.Abstractions.Models;

namespace Aero.Cms.Shared.Services;

/// <summary>
/// Simple abstraction over browser localStorage for testability and DI.
/// The WASM client registers a <see cref="Microsoft.JSInterop.ILocalStorageService"/>-backed implementation.
/// </summary>
public interface IAdminStorage
{
    T? GetItem<T>(string key);
    void SetItem<T>(string key, T value);
}

/// <summary>
/// Singleton client-side state container for the admin manager panel.
/// Replaces per-page HTTP calls to <c>GET /api/v1/admin/sites/current</c>
/// with in-memory state backed by localStorage persistence.
///
/// Based on Microsoft's official Blazor state container pattern:
/// https://learn.microsoft.com/en-us/aspnet/core/blazor/state-management/?view=aspnetcore-10.0#in-memory-state-container-service
/// </summary>
public sealed class AdminStateContainer
{
    private const string StorageKey = "aero-admin-state";
    private readonly IAdminStorage _storage;

    public AdminStateContainer(IAdminStorage storage)
    {
        _storage = storage;
    }

    /// <summary>
    /// Fired whenever state changes. Components subscribe via
    /// <c>StateChanged += StateHasChanged</c> and unsubscribe in <c>Dispose()</c>.
    /// </summary>
    public event Action? StateChanged;

    public long? CurrentSiteId { get; private set; }
    public string? CurrentSiteName { get; private set; }
    public string? CurrentView { get; set; }

    /// <summary>
    /// True after <see cref="LoadFromStorageAsync(ILocalStorageService)"/> has completed.
    /// </summary>
    public bool IsInitialized { get; private set; }

    /// <summary>
    /// Sets the current site and persists to localStorage.
    /// </summary>
    public void SetSite(long siteId, string siteName)
    {
        if (CurrentSiteId == siteId && CurrentSiteName == siteName)
            return;

        CurrentSiteId = siteId;
        CurrentSiteName = siteName;
        PersistToStorage();
        NotifyStateChanged();
    }

    /// <summary>
    /// Hydrates state from localStorage. Call once on app startup.
    /// </summary>
    public void LoadFromStorage()
    {
        try
        {
            var siteId = _storage.GetItem<long?>($"{StorageKey}.siteId");
            var siteName = _storage.GetItem<string>($"{StorageKey}.siteName");

            if (siteId.HasValue && siteId.Value > 0)
            {
                CurrentSiteId = siteId.Value;
                CurrentSiteName = siteName;
            }
        }
        catch
        {
            // localStorage unavailable — state starts empty; API fallback handled by caller
        }
        finally
        {
            IsInitialized = true;
        }
    }

    private void PersistToStorage()
    {
        try
        {
            _storage.SetItem($"{StorageKey}.siteId", CurrentSiteId);
            _storage.SetItem($"{StorageKey}.siteName", CurrentSiteName);
        }
        catch
        {
            // localStorage full or unavailable — non-critical, state lives in memory
        }
    }

    private void NotifyStateChanged() => StateChanged?.Invoke();
}
