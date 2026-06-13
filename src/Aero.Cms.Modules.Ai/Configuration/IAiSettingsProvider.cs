using Aero.Core;
using Aero.Core.Ai;
using Aero.Core.Railway;

namespace Aero.Cms.Modules.Ai.Configuration;

public interface IAiSettingsProvider
{
    Task<Result<AiRuntimeSettings>> GetAsync(
        string? providerId = null,
        CancellationToken cancellationToken = default);
}
