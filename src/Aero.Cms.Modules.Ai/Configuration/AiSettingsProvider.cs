using Aero.Core;
using Aero.Core.Ai;
using Aero.Core.Railway;

namespace Aero.Cms.Modules.Ai.Configuration;

/// <summary>
/// Adapts the persistent settings store to the runtime settings-provider contract.
/// </summary>
/// <param name="settingsStore">The store used to resolve and validate provider settings.</param>
public sealed class AiSettingsProvider(IAiSettingsStore settingsStore) : IAiSettingsProvider
{
    /// <inheritdoc />
public Task<Result<AiRuntimeSettings>> GetAsync(
        string? providerId = null,
        CancellationToken cancellationToken = default)
    {
        return settingsStore.GetRuntimeSettingsAsync(providerId, cancellationToken);
    }
}
