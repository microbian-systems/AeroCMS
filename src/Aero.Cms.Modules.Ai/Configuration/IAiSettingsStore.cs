using Aero.Cms.Abstractions.Ai;
using Aero.Core;
using Aero.Core.Ai;
using Aero.Core.Railway;

namespace Aero.Cms.Modules.Ai.Configuration;

public interface IAiSettingsStore
{
    Task<Result<AiSettingsConfiguration, AeroError>> GetConfigurationAsync(CancellationToken cancellationToken = default);

    Task<Result<AiSettingsConfiguration, AeroError>> SaveConfigurationAsync(
        SaveAiSettingsRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<AiProviderOption>, AeroError>> GetProviderOptionsAsync(CancellationToken cancellationToken = default);

    Task<Result<AiRuntimeSettings>> GetRuntimeSettingsAsync(
        string? providerId = null,
        CancellationToken cancellationToken = default);

    Task EnsureDefaultsAsync(CancellationToken cancellationToken = default);
}
