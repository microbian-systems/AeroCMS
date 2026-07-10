using Aero.Core;
using Aero.Core.Ai;
using Aero.Core.Railway;

namespace Aero.Cms.Modules.Ai.Configuration;

/// <summary>
/// Defines an interface for IAiSettingsProvider.
/// </summary>
public interface IAiSettingsProvider
{
        /// <summary>
    /// GetAsync method.
    /// </summary>
Task<Result<AiRuntimeSettings>> GetAsync(
        string? providerId = null,
        CancellationToken cancellationToken = default);
}
