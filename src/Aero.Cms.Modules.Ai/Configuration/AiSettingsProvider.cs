using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Modules.Ai.Configuration;

public sealed class AiSettingsProvider(IAiSettingsStore settingsStore) : IAiSettingsProvider
{
    public Task<Result<AiRuntimeSettings, AeroError>> GetAsync(
        string? providerId = null,
        CancellationToken cancellationToken = default)
    {
        return settingsStore.GetRuntimeSettingsAsync(providerId, cancellationToken);
    }
}
