using System.Globalization;
using System.Text.Json;
using Aero.Cms.Abstractions.Ai;
using Aero.Cms.Core.Models;
using Aero.Core;
using Aero.Core.Ai;
using Aero.Core.Railway;
using AeroDB;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Ai.Configuration;

public sealed class AiSettingsStore(
    IDocumentSession session,
    IConfiguration configuration,
    IAiSecretProtector secretProtector,
    ILogger<AiSettingsStore> logger) : IAiSettingsStore
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public async Task<Result<AiSettingsConfiguration, AeroError>> GetConfigurationAsync(
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await GetConfigurationCoreAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<Result<AiSettingsConfiguration, AeroError>> GetConfigurationCoreAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await EnsureDefaultsCoreAsync(cancellationToken);
            var enabled = await GetBoolSettingAsync(AiSettingKeys.Enabled, false, cancellationToken);
            var defaultProviderId = await GetStringSettingAsync(
                AiSettingKeys.DefaultProviderId,
                DefaultAiProviderProfiles.GetDefaultProviderId(configuration),
                cancellationToken) ?? DefaultAiProviderProfiles.OpenAiProviderId;
            var profiles = await LoadProfilesAsync(cancellationToken);

            if (profiles.All(profile => !profile.Id.Equals(defaultProviderId, StringComparison.OrdinalIgnoreCase)))
            {
                defaultProviderId = profiles.FirstOrDefault()?.Id ?? DefaultAiProviderProfiles.OpenAiProviderId;
            }

            return new AiSettingsConfiguration(
                enabled,
                defaultProviderId,
                profiles.Select(profile => ToSettings(profile, defaultProviderId)).ToList());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load AI settings configuration.");
            return AeroError.ConfigurationError("AI settings could not be loaded.");
        }
    }

    public async Task<Result<AiSettingsConfiguration, AeroError>> SaveConfigurationAsync(
        SaveAiSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await SaveConfigurationCoreAsync(request, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<Result<AiSettingsConfiguration, AeroError>> SaveConfigurationCoreAsync(
        SaveAiSettingsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await EnsureDefaultsCoreAsync(cancellationToken);

            var existing = (await LoadProfilesAsync(cancellationToken))
                .ToDictionary(profile => profile.Id, StringComparer.OrdinalIgnoreCase);
            var updatedProfiles = new List<AiProviderProfile>();

            foreach (var update in request.Providers)
            {
                if (string.IsNullOrWhiteSpace(update.Id))
                {
                    return AeroError.ValidationError(["Provider id is required."]);
                }

                var existingProfile = existing.GetValueOrDefault(update.Id);
                var protectedApiKey = existingProfile?.ProtectedApiKey;

                if (update.ClearApiKey)
                {
                    protectedApiKey = null;
                }

                if (!string.IsNullOrWhiteSpace(update.ApiKey))
                {
                    protectedApiKey = secretProtector.Protect(update.ApiKey);
                }

                var supportsContentEnhancement = SupportsContentEnhancement(update.Provider);

                var profile = new AiProviderProfile(
                    update.Id.Trim(),
                    string.IsNullOrWhiteSpace(update.DisplayName) ? update.Provider.ToString() : update.DisplayName.Trim(),
                    update.Provider,
                    update.Enabled,
                    Normalize(update.Endpoint),
                    Normalize(update.Model),
                    protectedApiKey,
                    Math.Clamp(update.Temperature, 0f, 2f),
                    Math.Clamp(update.MaxOutputTokens, 128, 32_000),
                    Math.Clamp(update.TimeoutSeconds, 1, 300),
                    update.StreamResponses,
                    update.SaveUsageTelemetry,
                    supportsContentEnhancement);

                updatedProfiles.Add(profile);
                StoreProfile(profile);
            }

            var defaultProviderId = string.IsNullOrWhiteSpace(request.DefaultProviderId)
                ? updatedProfiles.FirstOrDefault()?.Id ?? DefaultAiProviderProfiles.OpenAiProviderId
                : request.DefaultProviderId.Trim();

            if (updatedProfiles.All(profile => !profile.Id.Equals(defaultProviderId, StringComparison.OrdinalIgnoreCase)))
            {
                return AeroError.ValidationError(["Default provider must match one configured provider."]);
            }

            StoreSetting(AiSettingKeys.Enabled, request.Enabled.ToString(CultureInfo.InvariantCulture), "bool");
            StoreSetting(AiSettingKeys.DefaultProviderId, defaultProviderId, "string");
            StoreSetting(AiSettingKeys.ProviderIds, JsonSerializer.Serialize(updatedProfiles.Select(x => x.Id), JsonOptions), "json");

            await session.SaveChangesAsync(cancellationToken);
            return await GetConfigurationCoreAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save AI settings configuration.");
            return AeroError.ConfigurationError("AI settings could not be saved.");
        }
    }

    public async Task<Result<IReadOnlyList<AiProviderOption>, AeroError>> GetProviderOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await GetProviderOptionsCoreAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<Result<IReadOnlyList<AiProviderOption>, AeroError>> GetProviderOptionsCoreAsync(
        CancellationToken cancellationToken)
    {
        var configResult = await GetConfigurationCoreAsync(cancellationToken);
        if (configResult is Result<AiSettingsConfiguration, AeroError>.Failure failure)
        {
            return failure.Error;
        }

        var config = ((Result<AiSettingsConfiguration, AeroError>.Ok)configResult).Value;
        var options = config.Providers
            .Where(provider => provider.Enabled && provider.SupportsContentEnhancement && IsConfigured(provider))
            .Select(provider => new AiProviderOption(
                provider.Id,
                provider.DisplayName,
                provider.Provider,
                provider.Model,
                provider.Id.Equals(config.DefaultProviderId, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        return options;
    }

    public async Task<Result<AiRuntimeSettings>> GetRuntimeSettingsAsync(
        string? providerId = null,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await GetRuntimeSettingsCoreAsync(providerId, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<Result<AiRuntimeSettings>> GetRuntimeSettingsCoreAsync(
        string? providerId,
        CancellationToken cancellationToken)
    {
        try
        {
            var configResult = await GetConfigurationCoreAsync(cancellationToken);
            if (configResult is Result<AiSettingsConfiguration, AeroError>.Failure failure)
            {
                return failure.Error;
            }

            var config = ((Result<AiSettingsConfiguration, AeroError>.Ok)configResult).Value;
            if (!config.Enabled)
            {
                return AeroError.ConfigurationError("AI is disabled.");
            }

            var requestedProviderId = string.IsNullOrWhiteSpace(providerId)
                ? config.DefaultProviderId
                : providerId.Trim();

            var profiles = await LoadProfilesAsync(cancellationToken);
            var profile = profiles.FirstOrDefault(x => x.Id.Equals(requestedProviderId, StringComparison.OrdinalIgnoreCase));
            if (profile is null)
            {
                return AeroError.NotFoundError($"AI provider '{requestedProviderId}' was not found.");
            }

            if (!profile.Enabled)
            {
                return AeroError.ConfigurationError($"AI provider '{profile.DisplayName}' is disabled.");
            }

            if (!profile.SupportsContentEnhancement)
            {
                return AeroError.ConfigurationError($"AI provider '{profile.DisplayName}' does not support content enhancement yet.");
            }

            var apiKey = string.IsNullOrWhiteSpace(profile.ProtectedApiKey)
                ? GetConfiguredApiKey(profile)
                : secretProtector.Unprotect(profile.ProtectedApiKey);

            if (string.IsNullOrWhiteSpace(profile.Model))
            {
                return AeroError.ConfigurationError($"AI provider '{profile.DisplayName}' does not have a model configured.");
            }

            if (string.IsNullOrWhiteSpace(profile.Endpoint) && string.IsNullOrWhiteSpace(apiKey))
            {
                return AeroError.ConfigurationError($"AI provider '{profile.DisplayName}' needs an endpoint or API key.");
            }

            return new AiRuntimeSettings(
                profile.Id,
                profile.DisplayName,
                profile.Enabled,
                profile.Provider,
                profile.Endpoint,
                profile.Model,
                apiKey,
                profile.Temperature,
                profile.MaxOutputTokens,
                profile.TimeoutSeconds,
                profile.StreamResponses,
                profile.SaveUsageTelemetry,
                profile.SupportsContentEnhancement);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to resolve AI runtime settings for provider {ProviderId}.", providerId);
            return AeroError.ConfigurationError("AI runtime settings could not be resolved.");
        }
    }

    public async Task EnsureDefaultsAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureDefaultsCoreAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task EnsureDefaultsCoreAsync(CancellationToken cancellationToken)
    {
        var defaultProfiles = DefaultAiProviderProfiles.Create(configuration);
        var providerIdsSetting = await session.LoadAsync<Setting>(AiSettingKeys.ProviderIds, cancellationToken);
        var providerIds = providerIdsSetting is null
            ? defaultProfiles.Select(profile => profile.Id).ToList()
            : ParseProviderIds(providerIdsSetting.Value, defaultProfiles);

        foreach (var defaultProfile in defaultProfiles)
        {
            if (!providerIds.Contains(defaultProfile.Id, StringComparer.OrdinalIgnoreCase))
            {
                providerIds.Add(defaultProfile.Id);
            }

            var setting = await session.LoadAsync<Setting>(ProfileKey(defaultProfile.Id), cancellationToken);
            if (setting is not null)
            {
                continue;
            }

            var apiKey = GetConfiguredApiKey(defaultProfile);
            var profile = string.IsNullOrWhiteSpace(apiKey)
                ? defaultProfile
                : defaultProfile with { ProtectedApiKey = secretProtector.Protect(apiKey) };

            StoreProfile(profile);
        }

        if (await session.LoadAsync<Setting>(AiSettingKeys.Enabled, cancellationToken) is null)
        {
            StoreSetting(AiSettingKeys.Enabled, GetBoolConfiguration("Ai:Enabled", false).ToString(CultureInfo.InvariantCulture), "bool");
        }

        if (await session.LoadAsync<Setting>(AiSettingKeys.DefaultProviderId, cancellationToken) is null)
        {
            StoreSetting(AiSettingKeys.DefaultProviderId, DefaultAiProviderProfiles.GetDefaultProviderId(configuration), "string");
        }

        var providerIdsSnapshot = providerIds
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        StoreSetting(AiSettingKeys.ProviderIds, JsonSerializer.Serialize(providerIdsSnapshot, JsonOptions), "json");
        await session.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<AiProviderProfile>> LoadProfilesAsync(CancellationToken cancellationToken)
    {
        var defaultProfiles = DefaultAiProviderProfiles.Create(configuration);
        var providerIds = ParseProviderIds(
            (await session.LoadAsync<Setting>(AiSettingKeys.ProviderIds, cancellationToken))?.Value,
            defaultProfiles);
        var profiles = new List<AiProviderProfile>();

        foreach (var providerId in providerIds)
        {
            var setting = await session.LoadAsync<Setting>(ProfileKey(providerId), cancellationToken);
            if (setting is null)
            {
                var defaultProfile = defaultProfiles.FirstOrDefault(x => x.Id.Equals(providerId, StringComparison.OrdinalIgnoreCase));
                if (defaultProfile is not null)
                {
                    profiles.Add(defaultProfile);
                }

                continue;
            }

            var profile = JsonSerializer.Deserialize<AiProviderProfile>(setting.Value, JsonOptions);
            if (profile is not null)
            {
                // Recompute derived property — stored value may be stale after provider support changes.
                profiles.Add(profile with { SupportsContentEnhancement = SupportsContentEnhancement(profile.Provider) });
            }
        }

        return profiles
            .OrderBy(profile => profile.Provider)
            .ThenBy(profile => profile.DisplayName)
            .ToList();
    }

    private static AiProviderSettings ToSettings(AiProviderProfile profile, string defaultProviderId)
        => new(
            profile.Id,
            profile.DisplayName,
            profile.Provider,
            profile.Enabled,
            profile.Id.Equals(defaultProviderId, StringComparison.OrdinalIgnoreCase),
            profile.Endpoint,
            profile.Model,
            profile.HasApiKey,
            profile.Temperature,
            profile.MaxOutputTokens,
            profile.TimeoutSeconds,
            profile.StreamResponses,
            profile.SaveUsageTelemetry,
            profile.SupportsContentEnhancement);

    private static bool IsConfigured(AiProviderSettings provider)
        => !string.IsNullOrWhiteSpace(provider.Model)
            && (!string.IsNullOrWhiteSpace(provider.Endpoint) || provider.HasApiKey);

    private static bool SupportsContentEnhancement(AiProviderKind provider)
        => provider is not AiProviderKind.Future;

    private void StoreProfile(AiProviderProfile profile)
    {
        StoreSetting(ProfileKey(profile.Id), JsonSerializer.Serialize(profile, JsonOptions), "json");
    }

    private void StoreSetting(string key, string value, string type)
    {
        var setting = new Setting
        {
            Key = key,
            Value = value,
            Category = AiSettingKeys.Category,
            Type = type,
            ModifiedOn = DateTimeOffset.UtcNow
        };

        session.Store(setting);
    }

    private async Task<string?> GetStringSettingAsync(string key, string? fallback, CancellationToken cancellationToken)
    {
        var setting = await session.LoadAsync<Setting>(key, cancellationToken);
        return string.IsNullOrWhiteSpace(setting?.Value) ? fallback : setting.Value.Trim();
    }

    private async Task<bool> GetBoolSettingAsync(string key, bool fallback, CancellationToken cancellationToken)
    {
        var value = await GetStringSettingAsync(key, null, cancellationToken);
        return bool.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private string? GetConfiguredApiKey(AiProviderProfile profile)
    {
        var providerName = profile.Provider.ToString();
        return configuration[$"Ai:Providers:{providerName}:ApiKey"]
            ?? configuration[$"Ai:Providers:{profile.Id}:ApiKey"]
            ?? configuration["Ai:DefaultApiKey"];
    }

    private bool GetBoolConfiguration(string key, bool fallback)
        => bool.TryParse(configuration[key], out var value) ? value : fallback;

    private static List<string> ParseProviderIds(string? raw, IReadOnlyList<AiProviderProfile> defaults)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaults.Select(profile => profile.Id).ToList();
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(raw, JsonOptions)?
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? defaults.Select(profile => profile.Id).ToList();
        }
        catch
        {
            return defaults.Select(profile => profile.Id).ToList();
        }
    }

    private static string ProfileKey(string providerId)
        => $"{AiSettingKeys.ProviderPrefix}{providerId}";

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
