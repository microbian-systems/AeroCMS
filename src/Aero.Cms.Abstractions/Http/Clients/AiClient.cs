namespace Aero.Cms.Abstractions.Http.Clients;

using Aero.Cms.Abstractions.Ai;
using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

/// <summary>
/// Interface for AeroCMS AI manager endpoints.
/// </summary>
public interface IAiHttpClient
{
    /// <summary>
    /// Gets the AI provider configuration used by manager features.
    /// </summary>
    Task<Result<AiSettingsConfiguration, AeroError>> GetSettingsAsync(CancellationToken ct = default);

    /// <summary>
    /// Saves the AI provider configuration. API keys are write-only.
    /// </summary>
    Task<Result<AiSettingsConfiguration, AeroError>> SaveSettingsAsync(
        SaveAiSettingsRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Gets enabled and usable provider choices for content enhancement.
    /// </summary>
    Task<Result<IReadOnlyList<AiProviderOption>, AeroError>> GetProviderOptionsAsync(CancellationToken ct = default);

    /// <summary>
    /// Enhances one content field and returns a suggestion for review.
    /// </summary>
    Task<Result<EnhanceContentResponse, AeroError>> EnhanceContentAsync(
        EnhanceContentRequest request,
        CancellationToken ct = default);
}

/// <summary>
/// Typed client for AI manager endpoints.
/// </summary>
public sealed class AiHttpClient(HttpClient httpClient, ILogger<AiHttpClient> logger)
    : AeroCmsClientBase(httpClient, logger), IAiHttpClient
{
    /// <inheritdoc />
    public override string Path => "admin/ai";

    /// <inheritdoc />
    public Task<Result<AiSettingsConfiguration, AeroError>> GetSettingsAsync(CancellationToken ct = default)
    {
        return GetAsync<AiSettingsConfiguration>("settings", ct);
    }

    /// <inheritdoc />
    public Task<Result<AiSettingsConfiguration, AeroError>> SaveSettingsAsync(
        SaveAiSettingsRequest request,
        CancellationToken ct = default)
    {
        return PostAsync<SaveAiSettingsRequest, AiSettingsConfiguration>("settings", request, ct);
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<AiProviderOption>, AeroError>> GetProviderOptionsAsync(CancellationToken ct = default)
    {
        return GetAsync<IReadOnlyList<AiProviderOption>>("providers/options", ct);
    }

    /// <inheritdoc />
    public Task<Result<EnhanceContentResponse, AeroError>> EnhanceContentAsync(
        EnhanceContentRequest request,
        CancellationToken ct = default)
    {
        return PostAsync<EnhanceContentRequest, EnhanceContentResponse>("content/enhance", request, ct);
    }
}
