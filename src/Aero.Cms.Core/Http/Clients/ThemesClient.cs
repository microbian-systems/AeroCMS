namespace Aero.Cms.Core.Http.Clients;

using Microsoft.Extensions.Logging;

/// <summary>
/// Typed client for themes endpoints (stub implementation).
/// </summary>
public class ThemesClient : AeroClientBase
{
    protected override string ResourceName => "themes";

    public ThemesClient(HttpClient httpClient, ILogger<ThemesClient> logger)
        : base(httpClient, logger)
    {
    }

    public Task<IReadOnlyList<ThemeSummary>> GetAllAsync(CancellationToken ct = default)
    {
        return GetAsync<IReadOnlyList<ThemeSummary>>(string.Empty, ct) 
            ?? Task.FromResult<IReadOnlyList<ThemeSummary>>(Array.Empty<ThemeSummary>());
    }

    public Task<ThemeDetail?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        return GetAsync<ThemeDetail>($"details/{Uri.EscapeDataString(id)}", ct);
    }

    public Task<ThemeDetail?> GetCurrentAsync(CancellationToken ct = default)
    {
        return GetAsync<ThemeDetail>("current", ct);
    }

    public Task<ThemeDetail?> ActivateAsync(string id, CancellationToken ct = default)
    {
        return PostAsync<ThemeDetail?, object>($"{id}/activate", new object(), ct) 
            ?? Task.FromResult<ThemeDetail?>(null);
    }

    public Task<ThemeDetail?> UploadAsync(UploadThemeRequest request, CancellationToken ct = default)
    {
        return PostAsync<ThemeDetail?, UploadThemeRequest>(string.Empty, request, ct);
    }

    public Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        return DeleteAsync(id.ToString(), ct);
    }
}

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

public record ThemeSummary(string Id, string Name, string Version, string Author, string? ThumbnailUrl, bool IsActive);
public record ThemeDetail(string Id, string Name, string Version, string Author, string Description, string? ThumbnailUrl, bool IsActive, IReadOnlyList<ThemeAsset> Assets, DateTime InstalledAt);
public record ThemeAsset(string Path, string Type);
public record UploadThemeRequest(string Name, string Version, string Author, string Description, long FileSize, string? Content);
