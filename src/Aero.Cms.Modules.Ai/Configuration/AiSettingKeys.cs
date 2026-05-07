namespace Aero.Cms.Modules.Ai.Configuration;

internal static class AiSettingKeys
{
    public const string Category = "AI";
    public const string Enabled = "Ai.Enabled";
    public const string DefaultProviderId = "Ai.DefaultProviderId";
    public const string ProviderIds = "Ai.ProviderIds";
    public const string ProviderPrefix = "Ai.Provider.";
    public const string Provider = "Ai.Provider";
    public const string Endpoint = "Ai.Endpoint";
    public const string Model = "Ai.Model";
    public const string ApiKeySecretName = "Ai.ApiKeySecretName";
    public const string ApiKeyEnvironmentVariable = "Ai.ApiKeyEnvironmentVariable";
    public const string Temperature = "Ai.Temperature";
    public const string MaxOutputTokens = "Ai.MaxOutputTokens";
    public const string TimeoutSeconds = "Ai.TimeoutSeconds";
    public const string StreamResponses = "Ai.StreamResponses";
    public const string SaveUsageTelemetry = "Ai.SaveUsageTelemetry";

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
