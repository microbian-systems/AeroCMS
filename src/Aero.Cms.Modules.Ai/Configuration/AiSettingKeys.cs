namespace Aero.Cms.Modules.Ai.Configuration;

/// <summary>
/// Defines persisted setting keys owned by the AI module.
/// </summary>
internal static class AiSettingKeys
{
        /// <summary>
    /// Identifies AI settings in the shared settings store.
    /// </summary>
public const string Category = "AI";
        /// <summary>
    /// Stores whether AI operations are globally enabled.
    /// </summary>
public const string Enabled = "Ai.Enabled";
        /// <summary>
    /// Stores the identifier selected when a request does not specify a provider.
    /// </summary>
public const string DefaultProviderId = "Ai.DefaultProviderId";
        /// <summary>
    /// Stores the JSON array of configured provider-profile identifiers.
    /// </summary>
public const string ProviderIds = "Ai.ProviderIds";
        /// <summary>
    /// Prefixes setting keys that contain serialized provider profiles.
    /// </summary>
public const string ProviderPrefix = "Ai.Provider.";
        /// <summary>
    /// Names the legacy or flat provider-kind setting.
    /// </summary>
public const string Provider = "Ai.Provider";
        /// <summary>
    /// Names the legacy or flat provider-endpoint setting.
    /// </summary>
public const string Endpoint = "Ai.Endpoint";
        /// <summary>
    /// Names the legacy or flat model setting.
    /// </summary>
public const string Model = "Ai.Model";
        /// <summary>
    /// Names the legacy or flat secret-store reference setting.
    /// </summary>
public const string ApiKeySecretName = "Ai.ApiKeySecretName";
        /// <summary>
    /// Names the legacy or flat API-key environment-variable setting.
    /// </summary>
public const string ApiKeyEnvironmentVariable = "Ai.ApiKeyEnvironmentVariable";
        /// <summary>
    /// Names the legacy or flat sampling-temperature setting.
    /// </summary>
public const string Temperature = "Ai.Temperature";
        /// <summary>
    /// Names the legacy or flat output-token limit setting.
    /// </summary>
public const string MaxOutputTokens = "Ai.MaxOutputTokens";
        /// <summary>
    /// Names the legacy or flat request-timeout setting.
    /// </summary>
public const string TimeoutSeconds = "Ai.TimeoutSeconds";
        /// <summary>
    /// Names the legacy or flat response-streaming preference.
    /// </summary>
public const string StreamResponses = "Ai.StreamResponses";
        /// <summary>
    /// Names the legacy or flat usage-telemetry preference.
    /// </summary>
public const string SaveUsageTelemetry = "Ai.SaveUsageTelemetry";

        /// <summary>
    /// Gets the complete set of global and legacy flat setting keys.
    /// </summary>
    /// <remarks>Per-provider profile keys derived from <see cref="ProviderPrefix"/> are not enumerated.</remarks>
public static readonly IReadOnlyList<string> All =
    [
        Enabled,
        DefaultProviderId,
        ProviderIds,
        Provider,
        Endpoint,
        Model,
        ApiKeySecretName,
        ApiKeyEnvironmentVariable,
        Temperature,
        MaxOutputTokens,
        TimeoutSeconds,
        StreamResponses,
        SaveUsageTelemetry
    ];
}
