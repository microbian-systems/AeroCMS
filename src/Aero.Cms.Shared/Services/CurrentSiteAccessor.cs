using System.Net.Http.Json;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Models;

namespace Aero.Cms.Shared.Services;

/// <summary>
/// Blazor-friendly implementation of <see cref="ICurrentSiteAccessor"/>.
/// Uses HttpClient to call server API endpoints that manage the AeroCms.SiteId cookie.
/// Works in both InteractiveServer and InteractiveWebAssembly render modes.
/// </summary>
public sealed class CurrentSiteAccessor(HttpClient http) : ICurrentSiteAccessor
{
    public event Action? SiteChanged;

    public async Task<SiteViewModel?> GetCurrentSiteAsync()
    {
        try
        {
            var response = await http.GetAsync("/api/v1/admin/sites/current");
            if (!response.IsSuccessStatusCode)
                return null;
            return await response.Content.ReadFromJsonAsync<SiteViewModel>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<long?> GetCurrentSiteIdAsync()
    {
        var site = await GetCurrentSiteAsync();
        return site?.Id;
    }

    public async Task SetCurrentSiteAsync(long siteId)
    {
        try
        {
            await http.PostAsJsonAsync("/api/v1/admin/sites/current", siteId);
            SiteChanged?.Invoke();
        }
        catch
        {
            // Silently fail if circuit is disconnected
        }
    }

    public async Task ClearCurrentSiteAsync()
    {
        try
        {
            await http.DeleteAsync("/api/v1/admin/sites/current");
            SiteChanged?.Invoke();
        }
        catch
        {
            // Silently fail if circuit is disconnected
        }
    }
}
