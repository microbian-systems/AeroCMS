using System.Globalization;
using Aero.Cms.Abstractions.Ai;
using Aero.Cms.Core.Models;
using Aero.Core;
using Aero.Core.Railway;
using Marten;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Ai.Configuration;

public sealed class AiSettingsProvider(
    IDocumentSession session,
    IConfiguration configuration,
    ILogger<AiSettingsProvider> logger) : IAiSettingsProvider
{
    public async Task<Result<AiSettings, AeroError>> GetAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var stored = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in AiSettingKeys.All)
            {
                var setting = await session.LoadAsync<Setting>(key, cancellationToken);
                stored[key] = setting?.Value;
            }

            var settings = new AiSettings(
                Enabled: GetBool(stored, AiSettingKeys.Enabled, false),
                Provider: GetProvider(stored, AiSettingKeys.Provider, AiProviderKind.Tornado),
                Endpoint: GetString(stored, AiSettingKeys.Endpoint),
                Model: GetString(stored, AiSettingKeys.Model),
                ApiKeySecretName: GetString(stored, AiSettingKeys.ApiKeySecretName),
                ApiKeyEnvironmentVariable: GetString(stored, AiSettingKeys.ApiKeyEnvironmentVariable),
                Temperature: GetFloat(stored, AiSettingKeys.Temperature, 0.3f),
                MaxOutputTokens: GetInt(stored, AiSettingKeys.MaxOutputTokens, 1200),
                TimeoutSeconds: GetInt(stored, AiSettingKeys.TimeoutSeconds, 60),
                StreamResponses: GetBool(stored, AiSettingKeys.StreamResponses, false),
                SaveUsageTelemetry: GetBool(stored, AiSettingKeys.SaveUsageTelemetry, false));

            return settings;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load AI settings.");
            return AeroError.ConfigurationError("AI settings could not be loaded.");
        }
    }

    private string? GetString(IReadOnlyDictionary<string, string?> stored, string key)
    {
        var value = stored.TryGetValue(key, out var storedValue) && !string.IsNullOrWhiteSpace(storedValue)
            ? storedValue
            : configuration[key] ?? configuration[ToConfigurationPath(key)];

        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private bool GetBool(IReadOnlyDictionary<string, string?> stored, string key, bool fallback)
        => bool.TryParse(GetString(stored, key), out var value) ? value : fallback;

    private int GetInt(IReadOnlyDictionary<string, string?> stored, string key, int fallback)
        => int.TryParse(GetString(stored, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;

    private float GetFloat(IReadOnlyDictionary<string, string?> stored, string key, float fallback)
        => float.TryParse(GetString(stored, key), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;

    private AiProviderKind GetProvider(IReadOnlyDictionary<string, string?> stored, string key, AiProviderKind fallback)
    {
        var raw = GetString(stored, key);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        var normalized = raw.Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal);

        return Enum.TryParse<AiProviderKind>(normalized, ignoreCase: true, out var provider)
            ? provider
            : fallback;
    }

    private static string ToConfigurationPath(string key)
        => key.Replace('.', ':');
}
