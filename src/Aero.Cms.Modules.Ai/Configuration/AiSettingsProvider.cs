using Aero.Core;
using Aero.Core.Ai;
using Aero.Core.Railway;

namespace Aero.Cms.Modules.Ai.Configuration;

/// <summary>
/// Represents a class for AiSettingsProvider.
/// </summary>
public sealed class AiSettingsProvider(IAiSettingsStore settingsStore) : IAiSettingsProvider
{
        /// <summary>
    /// GetAsync method.
    /// </summary>
public Task<Result<AiRuntimeSettings>> GetAsync(
        string? providerId = null,
        CancellationToken cancellationToken = default)
    {
        return settingsStore.GetRuntimeSettingsAsync(providerId, cancellationToken);
    }
}
