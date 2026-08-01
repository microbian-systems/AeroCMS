using Aero.Core;
using Aero.Core.Ai;
using Aero.Core.Railway;

namespace Aero.Cms.Modules.Ai.Configuration;

/// <summary>
/// Resolves a configured provider profile into settings used for an AI invocation.
/// </summary>
public interface IAiSettingsProvider
{
        /// <summary>
    /// Resolves runtime settings for a requested provider or the configured default.
    /// </summary>
    /// <param name="providerId">
    /// The provider-profile identifier, or <see langword="null"/> or white space to select the configured default.
    /// </param>
    /// <param name="cancellationToken">A token that cancels settings lookup.</param>
    /// <returns>
    /// A successful result containing runtime settings, including the plaintext provider credential
    /// when configured; otherwise, a failure describing disabled, missing, unsupported, or incomplete configuration.
    /// </returns>
    /// <remarks>
    /// Callers must treat successful runtime settings as sensitive because they can contain an API key.
    /// </remarks>
Task<Result<AiRuntimeSettings>> GetAsync(
        string? providerId = null,
        CancellationToken cancellationToken = default);
}
