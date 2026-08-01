namespace Aero.Cms.Abstractions.Http.Clients;

using Aero.Cms.Abstractions.Theming;
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
    /// <param name="version">The exact installed theme version.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The theme detail or an error.</returns>
    Task<Result<ThemeDetail, AeroError>> GetByIdAsync(string id, string version, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ThemeDefinitionView>, AeroError>> ListDraftsAsync(CancellationToken ct = default);
    Task<Result<ThemeDefinitionView, AeroError>> GetDraftAsync(long id, CancellationToken ct = default);
    Task<Result<ThemeDefinitionView, AeroError>> CreateDraftAsync(CreateThemeCommand command, CancellationToken ct = default);
    Task<Result<ThemeDefinitionView, AeroError>> SaveDraftAsync(long id, SaveThemeDraftCommand command, CancellationToken ct = default);
    Task<Result<ThemePreviewView, AeroError>> CreatePreviewAsync(long id, CancellationToken ct = default);
    Task<Result<ThemeVersionView, AeroError>> PublishAsync(long id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ThemeVersionView>, AeroError>> ListVersionsAsync(long id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<SiteThemePublicationView>, AeroError>> GetPublicationHistoryAsync(CancellationToken ct = default);
    Task<Result<SiteThemePublicationView, AeroError>> AssignAsync(AssignThemeCommand command, CancellationToken ct = default);
    Task<Result<ThemeDefinitionView, AeroError>> ImportAsync(ThemeImportEnvelope envelope, CancellationToken ct = default);
    Task<Result<ThemeImportEnvelope, AeroError>> ExportAsync(long id, CancellationToken ct = default);
}

/// <summary>
/// Typed client for themes endpoints.
/// </summary>
public class ThemesHttpClient(HttpClient httpClient, ILogger<ThemesHttpClient> logger)
    : AeroCmsClientBase(httpClient, logger), IThemesHttpClient
{
    /// <inheritdoc />
    public override string Path => "admin/themes";

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<ThemeSummary>, AeroError>> GetAllAsync(CancellationToken ct = default)
    {
        return GetAsync<IReadOnlyList<ThemeSummary>>(string.Empty, ct);
    }

    /// <inheritdoc />
    public Task<Result<ThemeDetail, AeroError>> GetByIdAsync(string id, string version, CancellationToken ct = default)
    {
        return GetAsync<ThemeDetail>($"details/{Uri.EscapeDataString(id)}/{Uri.EscapeDataString(version)}", ct);
    }
    public Task<Result<IReadOnlyList<ThemeDefinitionView>, AeroError>> ListDraftsAsync(CancellationToken ct = default) => GetAsync<IReadOnlyList<ThemeDefinitionView>>("drafts", ct);
    public Task<Result<ThemeDefinitionView, AeroError>> GetDraftAsync(long id, CancellationToken ct = default) => GetAsync<ThemeDefinitionView>($"drafts/{id}", ct);
    public Task<Result<ThemeDefinitionView, AeroError>> CreateDraftAsync(CreateThemeCommand command, CancellationToken ct = default) => PostAsync<CreateThemeCommand, ThemeDefinitionView>("drafts", command, ct);
    public Task<Result<ThemeDefinitionView, AeroError>> SaveDraftAsync(long id, SaveThemeDraftCommand command, CancellationToken ct = default) => PutAsync<SaveThemeDraftCommand, ThemeDefinitionView>($"drafts/{id}", command, ct);
    public Task<Result<ThemePreviewView, AeroError>> CreatePreviewAsync(long id, CancellationToken ct = default) => PostAsync<object, ThemePreviewView>($"drafts/{id}/preview", new(), ct);
    public Task<Result<ThemeVersionView, AeroError>> PublishAsync(long id, CancellationToken ct = default) => PostAsync<object, ThemeVersionView>($"drafts/{id}/publish", new(), ct);
    public Task<Result<IReadOnlyList<ThemeVersionView>, AeroError>> ListVersionsAsync(long id, CancellationToken ct = default) => GetAsync<IReadOnlyList<ThemeVersionView>>($"drafts/{id}/versions", ct);
    public Task<Result<IReadOnlyList<SiteThemePublicationView>, AeroError>> GetPublicationHistoryAsync(CancellationToken ct = default) => GetAsync<IReadOnlyList<SiteThemePublicationView>>("publication-history", ct);
    public Task<Result<SiteThemePublicationView, AeroError>> AssignAsync(AssignThemeCommand command, CancellationToken ct = default) => PostAsync<AssignThemeCommand, SiteThemePublicationView>("assign", command, ct);
    public Task<Result<ThemeDefinitionView, AeroError>> ImportAsync(ThemeImportEnvelope envelope, CancellationToken ct = default) => PostAsync<ThemeImportEnvelope, ThemeDefinitionView>("import", envelope, ct);
    public Task<Result<ThemeImportEnvelope, AeroError>> ExportAsync(long id, CancellationToken ct = default) => GetAsync<ThemeImportEnvelope>($"drafts/{id}/export", ct);
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
/// <param name="IsSafeDefault">Whether this is the deployment's safe fallback theme.</param>
public record ThemeSummary(string Id, string Name, string Version, string Author, string? ThumbnailUrl, bool IsSafeDefault);

/// <summary>
/// Detailed information for a theme.
/// </summary>
/// <param name="Id">The theme identifier.</param>
/// <param name="Name">The display name.</param>
/// <param name="Version">The version string.</param>
/// <param name="Author">The author name.</param>
/// <param name="Description">The detailed description.</param>
/// <param name="ThumbnailUrl">The optional thumbnail URL.</param>
/// <param name="IsSafeDefault">Whether this is the deployment's safe fallback theme.</param>
/// <param name="Assets">The list of theme assets.</param>
public record ThemeDetail(string Id, string Name, string Version, string Author, string Description, string? ThumbnailUrl, bool IsSafeDefault, IReadOnlyList<ThemeAsset> Assets);

/// <summary>
/// Information about a theme asset.
/// </summary>
/// <param name="Path">The relative path.</param>
/// <param name="Type">The asset type.</param>
public record ThemeAsset(string Path, string Type);
