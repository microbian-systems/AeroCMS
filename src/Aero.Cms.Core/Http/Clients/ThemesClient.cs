namespace Aero.Cms.Core.Http.Clients;

using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

public interface IThemesHttpClient
{
    Task<Result<string, IReadOnlyList<ThemeSummary>>> GetAllAsync(CancellationToken ct = default);
    Task<Result<string, ThemeDetail>> GetByIdAsync(string id, CancellationToken ct = default);
    Task<Result<string, ThemeDetail>> GetCurrentAsync(CancellationToken ct = default);
    Task<Result<string, ThemeDetail>> ActivateAsync(string id, CancellationToken ct = default);
    Task<Result<string, ThemeDetail>> UploadAsync(UploadThemeRequest request, CancellationToken ct = default);
    Task<Result<string, bool>> DeleteAsync(string id, CancellationToken ct = default);
}

/// <summary>
/// Typed client for themes endpoints (stub implementation).
/// </summary>
public class ThemesHttpClient(HttpClient httpClient, ILogger<ThemesHttpClient> logger)
    : AeroCmsClientBase(httpClient, logger), IThemesHttpClient
{
    protected override string ResourceName => "themes";

    public Task<Result<string, IReadOnlyList<ThemeSummary>>> GetAllAsync(CancellationToken ct = default)
    {
        return GetResultAsync<IReadOnlyList<ThemeSummary>>(string.Empty, ct);
    }

    public Task<Result<string, ThemeDetail>> GetByIdAsync(string id, CancellationToken ct = default)
    {
        return GetResultAsync<ThemeDetail>($"details/{Uri.EscapeDataString(id)}", ct);
    }

    public Task<Result<string, ThemeDetail>> GetCurrentAsync(CancellationToken ct = default)
    {
        return GetResultAsync<ThemeDetail>("current", ct);
    }

    public Task<Result<string, ThemeDetail>> ActivateAsync(string id, CancellationToken ct = default)
    {
        return PostResultAsync<object, ThemeDetail>($"{id}/activate", new object(), ct);
    }

    public Task<Result<string, ThemeDetail>> UploadAsync(UploadThemeRequest request, CancellationToken ct = default)
    {
        return PostResultAsync<UploadThemeRequest, ThemeDetail>(string.Empty, request, ct);
    }

    public Task<Result<string, bool>> DeleteAsync(string id, CancellationToken ct = default)
    {
        return DeleteResultAsync(id, ct);
    }
}

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

public record ThemeSummary(string Id, string Name, string Version, string Author, string? ThumbnailUrl, bool IsActive);
public record ThemeDetail(string Id, string Name, string Version, string Author, string Description, string? ThumbnailUrl, bool IsActive, IReadOnlyList<ThemeAsset> Assets, DateTime InstalledAt);
public record ThemeAsset(string Path, string Type);
public record UploadThemeRequest(string Name, string Version, string Author, string Description, long FileSize, string? Content);
