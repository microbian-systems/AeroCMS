using Aero.Cms.Abstractions.Ai;
using Aero.Core;
using Aero.Core.Ai;
using Aero.Core.Railway;

namespace Aero.Cms.Modules.Ai.Configuration;

/// <summary>
/// Persists AI provider profiles and resolves manager-safe and runtime configuration views.
/// </summary>
/// <remarks>
/// Manager-facing methods do not return plaintext API keys. Runtime settings can contain a recovered
/// credential and must be handled as sensitive data.
/// </remarks>
public interface IAiSettingsStore
{
        /// <summary>
    /// Loads the manager-safe AI configuration, creating missing default settings as needed.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels persistence operations.</param>
    /// <returns>
    /// A successful result containing global and provider settings without plaintext credentials,
    /// or a configuration failure when the settings cannot be loaded.
    /// </returns>
Task<Result<AiSettingsConfiguration, AeroError>> GetConfigurationAsync(CancellationToken cancellationToken = default);

        /// <summary>
    /// Replaces the configured provider-profile set and global AI selection.
    /// </summary>
    /// <param name="request">The complete settings update, including optional write-only API keys.</param>
    /// <param name="cancellationToken">A token that cancels persistence operations.</param>
    /// <returns>
    /// A successful manager-safe view of the saved configuration; otherwise, a validation or configuration failure.
    /// </returns>
    /// <remarks>
    /// New API keys are protected before persistence; omitted keys retain the previous protected value unless
    /// explicitly cleared. Numeric provider settings are clamped to supported ranges. Omitted custom profiles
    /// leave the provider-id index, while omitted built-in profiles are reintroduced when defaults are ensured
    /// during the returned configuration load. Setting documents for omitted profiles are not deleted.
    /// </remarks>
Task<Result<AiSettingsConfiguration, AeroError>> SaveConfigurationAsync(
        SaveAiSettingsRequest request,
        CancellationToken cancellationToken = default);

        /// <summary>
    /// Lists enabled and sufficiently configured providers supported for content operations.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels persistence operations.</param>
    /// <returns>A successful list of manager-safe provider choices, or a configuration failure.</returns>
Task<Result<IReadOnlyList<AiProviderOption>, AeroError>> GetProviderOptionsAsync(CancellationToken cancellationToken = default);

        /// <summary>
    /// Resolves provider settings for an AI invocation.
    /// </summary>
    /// <param name="providerId">
    /// The provider-profile identifier, or <see langword="null"/> or white space to select the configured default.
    /// </param>
    /// <param name="cancellationToken">A token that cancels persistence operations.</param>
    /// <returns>
    /// A successful result containing runtime settings and any recovered API key, or a failure when
    /// AI is disabled or the provider is missing, disabled, unsupported, or incomplete.
    /// </returns>
Task<Result<AiRuntimeSettings>> GetRuntimeSettingsAsync(
        string? providerId = null,
        CancellationToken cancellationToken = default);

        /// <summary>
    /// Creates missing global settings and built-in provider profiles.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels persistence operations.</param>
    /// <returns>A task that completes after changes have been saved.</returns>
    /// <remarks>
    /// Configuration-sourced API keys are protected before being copied into newly created profile documents.
    /// Existing profiles are not overwritten, while missing built-in profile identifiers are added to the index.
    /// </remarks>
Task EnsureDefaultsAsync(CancellationToken cancellationToken = default);
}
