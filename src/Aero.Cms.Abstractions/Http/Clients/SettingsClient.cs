namespace Aero.Cms.Abstractions.Http.Clients;

using Aero.Core.Railway;
using Microsoft.Extensions.Logging;
using Orleans;

/// <summary>
/// Interface for settings HTTP client.
/// </summary>
public interface ISettingsHttpClient
{
    /// <summary>
    /// Gets all settings.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A list of setting summaries or an error.</returns>
    Task<Result<IReadOnlyList<SettingSummary>, AeroError>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets a setting by its identifier key.
    /// </summary>
    /// <param name="key">The setting key.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The setting detail or an error.</returns>
    Task<Result<SettingDetail, AeroError>> GetByKeyAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Gets settings in a specific category.
    /// </summary>
    /// <param name="category">The category name.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A list of setting details or an error.</returns>
    Task<Result<IReadOnlyList<SettingDetail>, AeroError>> GetByCategoryAsync(string category, CancellationToken ct = default);

    /// <summary>
    /// Creates or updates a setting.
    /// </summary>
    /// <param name="request">The set setting request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The updated setting detail or an error.</returns>
    Task<Result<SettingDetail, AeroError>> SetAsync(SetSettingRequest request, CancellationToken ct = default);

    /// <summary>
    /// Deletes a setting by its identifier key.
    /// </summary>
    /// <param name="key">The setting key to delete.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True if deletion was successful or an error.</returns>
    Task<Result<bool, AeroError>> DeleteAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Gets all setting categories.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A list of setting categories or an error.</returns>
    Task<Result<IReadOnlyList<SettingCategory>, AeroError>> GetCategoriesAsync(CancellationToken ct = default);
}

/// <summary>
/// Typed client for settings endpoints.
/// </summary>
public class SettingsHttpClient(HttpClient httpClient, ILogger<SettingsHttpClient> logger) : AeroCmsClientBase(httpClient, logger), ISettingsHttpClient
{
    /// <inheritdoc />
    public override string Path => "admin/settings";

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<SettingSummary>, AeroError>> GetAllAsync(CancellationToken ct = default)
    {
        return GetAsync<IReadOnlyList<SettingSummary>>(string.Empty, ct);
    }

    /// <inheritdoc />
    public Task<Result<SettingDetail, AeroError>> GetByKeyAsync(string key, CancellationToken ct = default)
    {
        return GetAsync<SettingDetail>($"key/{Uri.EscapeDataString(key)}", ct);
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<SettingDetail>, AeroError>> GetByCategoryAsync(string category, CancellationToken ct = default)
    {
        return GetAsync<IReadOnlyList<SettingDetail>>($"category/{Uri.EscapeDataString(category)}", ct);
    }

    /// <inheritdoc />
    public Task<Result<SettingDetail, AeroError>> SetAsync(SetSettingRequest request, CancellationToken ct = default)
    {
        return PostAsync<SetSettingRequest, SettingDetail>(string.Empty, request, ct);
    }

    /// <inheritdoc />
    public Task<Result<bool, AeroError>> DeleteAsync(string key, CancellationToken ct = default)
    {
        return MapBoolResult(base.DeleteAsync($"key/{Uri.EscapeDataString(key)}", ct));
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<SettingCategory>, AeroError>> GetCategoriesAsync(CancellationToken ct = default)
    {
        return GetAsync<IReadOnlyList<SettingCategory>>("categories", ct);
    }

    private static async Task<Result<bool, AeroError>> MapBoolResult(Task<Result<HttpResponseMessage, AeroError>> task)
    {
        var response = await task;
        return response switch
        {
            Result<HttpResponseMessage, AeroError>.Ok => true,
            Result<HttpResponseMessage, AeroError>.Failure(var error) => error,
            _ => AeroError.CreateError("Unexpected result from HTTP operation")
        };
    }
}

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

/// <summary>
/// Summary information for a setting.
/// </summary>
[Alias("SettingSummary")]
[GenerateSerializer]
public record SettingSummary(
    [property: Id(0)] string Key,
    [property: Id(1)] string Category,
    [property: Id(2)] string? Description
);

/// <summary>
/// Detailed information for a setting.
/// </summary>
[Alias("SettingDetail")]
[GenerateSerializer]
public record SettingDetail(
    [property: Id(0)] string Key,
    [property: Id(1)] string Value,
    [property: Id(2)] string Category,
    [property: Id(3)] string? Description,
    [property: Id(4)] string Type,
    [property: Id(5)] DateTime UpdatedAt
);

/// <summary>
/// Request to create or update a setting.
/// </summary>
[Alias("SetSettingRequest")]
[GenerateSerializer]
public record SetSettingRequest(
    [property: Id(0)] string Key,
    [property: Id(1)] string Value,
    [property: Id(2)] string Category,
    [property: Id(3)] string Type
);

/// <summary>
/// Information about a setting category.
/// </summary>
[Alias("SettingCategory")]
[GenerateSerializer]
public record SettingCategory(
    [property: Id(0)] string Name,
    [property: Id(1)] int SettingCount
);
