namespace Aero.Cms.Core.Http.Clients;

using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

/// <summary>
/// Interface for themes HTTP client.
/// </summary>
public interface IThemesHttpClient
{
    /// <summary>
    /// Gets all installed themes.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A list of theme summaries or an error.</returns>
    Task<Result<IReadOnlyList<ThemeSummary>, AeroError>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets detailed information for a specific theme.
    /// </summary>
    /// <param name="id">The theme identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The theme detail or an error.</returns>
    Task<Result<ThemeDetail, AeroError>> GetByIdAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Gets the current active theme.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The theme detail or an error.</returns>
    Task<Result<ThemeDetail, AeroError>> GetCurrentAsync(CancellationToken ct = default);

    /// <summary>
    /// Activates a specific theme.
    /// </summary>
    /// <param name="id">The theme identifier to activate.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The updated theme detail or an error.</returns>
    Task<Result<ThemeDetail, AeroError>> ActivateAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Uploads and installs a new theme.
    /// </summary>
    /// <param name="request">The upload theme request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The installed theme detail or an error.</returns>
    Task<Result<ThemeDetail, AeroError>> UploadAsync(UploadThemeRequest request, CancellationToken ct = default);

    /// <summary>
    /// Deletes (uninstalls) a theme.
    /// </summary>
    /// <param name="id">The theme identifier to delete.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True if deletion was successful or an error.</returns>
    Task<Result<bool, AeroError>> DeleteAsync(string id, CancellationToken ct = default);
}

/// <summary>
/// Typed client for themes endpoints.
/// </summary>
public class ThemesHttpClient(HttpClient httpClient, ILogger<ThemesHttpClient> logger)
    : AeroCmsClientBase(httpClient, logger), IThemesHttpClient
{
    /// <inheritdoc />
    protected override string ResourceName => "themes";

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<ThemeSummary>, AeroError>> GetAllAsync(CancellationToken ct = default)
    {
        return GetAsync<IReadOnlyList<ThemeSummary>>(string.Empty, ct);
    }

    /// <inheritdoc />
    public Task<Result<ThemeDetail, AeroError>> GetByIdAsync(string id, CancellationToken ct = default)
    {
        return GetAsync<ThemeDetail>($"details/{Uri.EscapeDataString(id)}", ct);
    }

    /// <inheritdoc />
    public Task<Result<ThemeDetail, AeroError>> GetCurrentAsync(CancellationToken ct = default)
    {
        return GetAsync<ThemeDetail>("current", ct);
    }

    /// <inheritdoc />
    public Task<Result<ThemeDetail, AeroError>> ActivateAsync(string id, CancellationToken ct = default)
    {
        return PostAsync<object, ThemeDetail>($"{id}/activate", new object(), ct);
    }

    /// <inheritdoc />
    public Task<Result<ThemeDetail, AeroError>> UploadAsync(UploadThemeRequest request, CancellationToken ct = default)
    {
        return PostAsync<UploadThemeRequest, ThemeDetail>(string.Empty, request, ct);
    }

    /// <inheritdoc />
    public Task<Result<bool, AeroError>> DeleteAsync(string id, CancellationToken ct = default)
    {
        return base.DeleteAsync(id, ct);
    }
}

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

/// <summary>
/// Summary information for a theme.
/// </summary>
/// <param name="Id">The theme identifier.</param>
/// <param name="Name">The display name.</param>
/// <param name="Version">The version string.</param>
/// <param name="Author">The author name.</param>
/// <param name="ThumbnailUrl">The optional thumbnail URL.</param>
/// <param name="IsActive">Whether this is the active theme.</param>
public record ThemeSummary(string Id, string Name, string Version, string Author, string? ThumbnailUrl, bool IsActive);

/// <summary>
/// Detailed information for a theme.
/// </summary>
/// <param name="Id">The theme identifier.</param>
/// <param name="Name">The display name.</param>
/// <param name="Version">The version string.</param>
/// <param name="Author">The author name.</param>
/// <param name="Description">The detailed description.</param>
/// <param name="ThumbnailUrl">The optional thumbnail URL.</param>
/// <param name="IsActive">Whether this is the active theme.</param>
/// <param name="Assets">The list of theme assets.</param>
/// <param name="InstalledAt">The installation time.</param>
public record ThemeDetail(string Id, string Name, string Version, string Author, string Description, string? ThumbnailUrl, bool IsActive, IReadOnlyList<ThemeAsset> Assets, DateTime InstalledAt);

/// <summary>
/// Information about a theme asset.
/// </summary>
/// <param name="Path">The relative path.</param>
/// <param name="Type">The asset type.</param>
public record ThemeAsset(string Path, string Type);

/// <summary>
/// Request to upload and install a new theme.
/// </summary>
/// <param name="Name">The display name.</param>
/// <param name="Version">The version string.</param>
/// <param name="Author">The author name.</param>
/// <param name="Description">The detailed description.</param>
/// <param name="FileSize">The file size in bytes.</param>
/// <param name="Content">The base64 encoded theme package content.</param>
public record UploadThemeRequest(string Name, string Version, string Author, string Description, long FileSize, string? Content);
