using Aero.Cms.Abstractions.Ai;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Modules.Ai.Configuration;

public interface IAiSettingsProvider
{
    Task<Result<AiRuntimeSettings, AeroError>> GetAsync(
        string? providerId = null,
        CancellationToken cancellationToken = default);
}
