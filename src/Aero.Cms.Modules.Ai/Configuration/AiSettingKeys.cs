namespace Aero.Cms.Modules.Ai.Configuration;

internal static class AiSettingKeys
{
        /// <summary>
    /// Category.
    /// </summary>
public const string Category = "AI";
        /// <summary>
    /// Enabled.
    /// </summary>
public const string Enabled = "Ai.Enabled";
        /// <summary>
    /// DefaultProviderId.
    /// </summary>
public const string DefaultProviderId = "Ai.DefaultProviderId";
        /// <summary>
    /// ProviderIds.
    /// </summary>
public const string ProviderIds = "Ai.ProviderIds";
        /// <summary>
    /// ProviderPrefix.
    /// </summary>
public const string ProviderPrefix = "Ai.Provider.";
        /// <summary>
    /// Provider.
    /// </summary>
public const string Provider = "Ai.Provider";
        /// <summary>
    /// Endpoint.
    /// </summary>
public const string Endpoint = "Ai.Endpoint";
        /// <summary>
    /// Model.
    /// </summary>
public const string Model = "Ai.Model";
        /// <summary>
    /// ApiKeySecretName.
    /// </summary>
public const string ApiKeySecretName = "Ai.ApiKeySecretName";
        /// <summary>
    /// ApiKeyEnvironmentVariable.
    /// </summary>
public const string ApiKeyEnvironmentVariable = "Ai.ApiKeyEnvironmentVariable";
        /// <summary>
    /// Temperature.
    /// </summary>
public const string Temperature = "Ai.Temperature";
        /// <summary>
    /// MaxOutputTokens.
    /// </summary>
public const string MaxOutputTokens = "Ai.MaxOutputTokens";
        /// <summary>
    /// TimeoutSeconds.
    /// </summary>
public const string TimeoutSeconds = "Ai.TimeoutSeconds";
        /// <summary>
    /// StreamResponses.
    /// </summary>
public const string StreamResponses = "Ai.StreamResponses";
        /// <summary>
    /// SaveUsageTelemetry.
    /// </summary>
public const string SaveUsageTelemetry = "Ai.SaveUsageTelemetry";

        /// <summary>
    /// All.
    /// </summary>
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
