namespace Aero.Cms.Core.Http.Clients;

using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

public interface ISettingsHttpClient
{
    Task<Result<string, IReadOnlyList<SettingSummary>>> GetAllAsync(CancellationToken ct = default);
    Task<Result<string, SettingDetail>> GetByKeyAsync(string key, CancellationToken ct = default);
    Task<Result<string, IReadOnlyList<SettingDetail>>> GetByCategoryAsync(string category, CancellationToken ct = default);
    Task<Result<string, SettingDetail>> SetAsync(SetSettingRequest request, CancellationToken ct = default);
    Task<Result<string, bool>> DeleteAsync(string key, CancellationToken ct = default);
    Task<Result<string, IReadOnlyList<SettingCategory>>> GetCategoriesAsync(CancellationToken ct = default);
}

/// <summary>
/// Typed client for settings endpoints (stub implementation).
/// </summary>
public class SettingsHttpClient(HttpClient httpClient, ILogger<SettingsHttpClient> logger) : AeroCmsClientBase(httpClient, logger), ISettingsHttpClient
{
    protected override string ResourceName => "settings";

    public Task<Result<string, IReadOnlyList<SettingSummary>>> GetAllAsync(CancellationToken ct = default)
    {
        return GetResultAsync<IReadOnlyList<SettingSummary>>(string.Empty, ct);
    }

    public Task<Result<string, SettingDetail>> GetByKeyAsync(string key, CancellationToken ct = default)
    {
        return GetResultAsync<SettingDetail>($"key/{Uri.EscapeDataString(key)}", ct);
    }

    public Task<Result<string, IReadOnlyList<SettingDetail>>> GetByCategoryAsync(string category, CancellationToken ct = default)
    {
        return GetResultAsync<IReadOnlyList<SettingDetail>>($"category/{Uri.EscapeDataString(category)}", ct);
    }

    public Task<Result<string, SettingDetail>> SetAsync(SetSettingRequest request, CancellationToken ct = default)
    {
        return PostResultAsync<SetSettingRequest, SettingDetail>(string.Empty, request, ct);
    }

    public Task<Result<string, bool>> DeleteAsync(string key, CancellationToken ct = default)
    {
        return DeleteResultAsync($"key/{Uri.EscapeDataString(key)}", ct);
    }

    public Task<Result<string, IReadOnlyList<SettingCategory>>> GetCategoriesAsync(CancellationToken ct = default)
    {
        return GetResultAsync<IReadOnlyList<SettingCategory>>("categories", ct);
    }
}

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

public record SettingSummary(string Key, string Category, string? Description);
public record SettingDetail(string Key, string Value, string Category, string? Description, string Type, DateTime UpdatedAt);
public record SetSettingRequest(string Key, string Value, string Category, string Type);
public record SettingCategory(string Name, int SettingCount);
