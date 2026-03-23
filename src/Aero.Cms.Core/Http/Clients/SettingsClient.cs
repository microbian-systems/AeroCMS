namespace Aero.Cms.Core.Http.Clients;

using Microsoft.Extensions.Logging;

/// <summary>
/// Typed client for settings endpoints (stub implementation).
/// </summary>
public class SettingsClient : AeroClientBase
{
    protected override string ResourceName => "settings";

    public SettingsClient(HttpClient httpClient, ILogger<SettingsClient> logger)
        : base(httpClient, logger)
    {
    }

    public Task<IReadOnlyList<SettingSummary>> GetAllAsync(CancellationToken ct = default)
    {
        return GetAsync<IReadOnlyList<SettingSummary>>(string.Empty, ct) 
            ?? Task.FromResult<IReadOnlyList<SettingSummary>>(Array.Empty<SettingSummary>());
    }

    public Task<SettingDetail?> GetByKeyAsync(string key, CancellationToken ct = default)
    {
        return GetAsync<SettingDetail>($"key/{Uri.EscapeDataString(key)}", ct);
    }

    public Task<IReadOnlyList<SettingDetail>> GetByCategoryAsync(string category, CancellationToken ct = default)
    {
        return GetAsync<IReadOnlyList<SettingDetail>>($"category/{Uri.EscapeDataString(category)}", ct) 
            ?? Task.FromResult<IReadOnlyList<SettingDetail>>(Array.Empty<SettingDetail>());
    }

    public Task<SettingDetail?> SetAsync(SetSettingRequest request, CancellationToken ct = default)
    {
        return PostAsync<SettingDetail?, SetSettingRequest>(string.Empty, request, ct);
    }

    public Task<bool> DeleteAsync(string key, CancellationToken ct = default)
    {
        return DeleteAsync($"key/{Uri.EscapeDataString(key)}", ct);
    }

    public Task<IReadOnlyList<SettingCategory>> GetCategoriesAsync(CancellationToken ct = default)
    {
        return GetAsync<IReadOnlyList<SettingCategory>>("categories", ct) 
            ?? Task.FromResult<IReadOnlyList<SettingCategory>>(Array.Empty<SettingCategory>());
    }
}

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

public record SettingSummary(string Key, string Category, string? Description);
public record SettingDetail(string Key, string Value, string Category, string? Description, string Type, DateTime UpdatedAt);
public record SetSettingRequest(string Key, string Value, string Category, string Type);
public record SettingCategory(string Name, int SettingCount);
