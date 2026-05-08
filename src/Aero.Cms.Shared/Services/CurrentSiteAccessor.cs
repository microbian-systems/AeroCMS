using System.Net.Http.Json;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Abstractions.Requests;
using Aero.Cms.Contracts.Models;
using CICurrentSiteAccessor = Aero.Cms.Contracts.Abstractions.ICurrentSiteAccessor;

namespace Aero.Cms.Shared.Services;

/// <summary>
/// Blazor-friendly implementation of <see cref="ICurrentSiteAccessor"/>.
/// Uses HttpClient to call server API endpoints that manage the AeroCms.SiteId cookie.
/// Works in both InteractiveServer and InteractiveWebAssembly render modes.
/// </summary>
public sealed class CurrentSiteAccessor(HttpClient http) : ICurrentSiteAccessor, CICurrentSiteAccessor
{
    public event Action? SiteChanged;

    private long? _cachedSiteId;

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

    async Task<SiteInfo?> CICurrentSiteAccessor.GetCurrentSiteAsync()
    {
        var vm = await GetCurrentSiteAsync();
        return vm is null ? null : MapToSiteInfo(vm);
    }

    public async Task<long?> GetCurrentSiteIdAsync()
    {
        var site = await GetCurrentSiteAsync();
        if (site is not null)
        {
            _cachedSiteId = site.Id;
            return site.Id;
        }
        // Fall back to in-memory cache when cookie/HTTP call fails
        return _cachedSiteId;
    }

    public async Task SetCurrentSiteAsync(long siteId)
    {
        try
        {
            var response = await http.PostAsJsonAsync("/api/v1/admin/sites/current", new SetCurrentSiteRequest(siteId));
            if (!response.IsSuccessStatusCode)
                return;

            _cachedSiteId = siteId;
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
            _cachedSiteId = null;
            SiteChanged?.Invoke();
        }
        catch
        {
            // Silently fail if circuit is disconnected
        }
    }

    private static SiteInfo MapToSiteInfo(SiteViewModel vm) => new(
        vm.Id, vm.Name, vm.PrimaryHost, vm.IsEnabled, vm.DefaultCulture, vm.TenantId);
}
