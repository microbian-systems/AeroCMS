using Aero.Cms.Abstractions.Ai;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Modules.Ai.Configuration;

public interface IAiSettingsProvider
{
    Task<Result<AiSettings, AeroError>> GetAsync(CancellationToken cancellationToken = default);
}
