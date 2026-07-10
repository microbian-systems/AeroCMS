using Aero.Cms.Abstractions.Ai;
using Aero.Core;
using Aero.Core.Ai;
using Aero.Core.Railway;

namespace Aero.Cms.Modules.Ai.Configuration;

/// <summary>
/// Defines an interface for IAiSettingsStore.
/// </summary>
public interface IAiSettingsStore
{
        /// <summary>
    /// GetConfigurationAsync method.
    /// </summary>
Task<Result<AiSettingsConfiguration, AeroError>> GetConfigurationAsync(CancellationToken cancellationToken = default);

        /// <summary>
    /// SaveConfigurationAsync method.
    /// </summary>
Task<Result<AiSettingsConfiguration, AeroError>> SaveConfigurationAsync(
        SaveAiSettingsRequest request,
        CancellationToken cancellationToken = default);

        /// <summary>
    /// GetProviderOptionsAsync method.
    /// </summary>
Task<Result<IReadOnlyList<AiProviderOption>, AeroError>> GetProviderOptionsAsync(CancellationToken cancellationToken = default);

        /// <summary>
    /// GetRuntimeSettingsAsync method.
    /// </summary>
Task<Result<AiRuntimeSettings>> GetRuntimeSettingsAsync(
        string? providerId = null,
        CancellationToken cancellationToken = default);

        /// <summary>
    /// EnsureDefaultsAsync method.
    /// </summary>
Task EnsureDefaultsAsync(CancellationToken cancellationToken = default);
}
