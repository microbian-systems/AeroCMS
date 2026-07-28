using System.Globalization;
using System.Text.Json;
using Aero.Cms.Abstractions.Ai;
using Aero.Cms.Core.Models;
using Aero.Core;
using Aero.Core.Ai;
using Aero.Core.Railway;
using AeroDB.Sable;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ContractAiProviderKind = Aero.Cms.Abstractions.Ai.AiProviderKind;
using CoreAiProviderKind = Aero.Core.Ai.AiProviderKind;

namespace Aero.Cms.Modules.Ai.Configuration;

/// <summary>
/// Stores AI configuration in Sable setting documents and resolves provider profiles for runtime use.
/// </summary>
/// <param name="session">The document session used to load and persist <see cref="Setting"/> documents.</param>
/// <param name="configuration">Host configuration used to seed defaults and resolve fallback API keys.</param>
/// <param name="secretProtector">The reversible protector used before API keys are persisted.</param>
/// <param name="logger">The logger for settings failures.</param>
/// <remarks>
/// Operations on one store instance are serialized with a semaphore because reads can create defaults
/// and writes reuse the same document session. The manager-safe configuration exposes only
/// <c>HasApiKey</c>; runtime settings can contain the recovered plaintext credential.
/// </remarks>
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

    /// <inheritdoc />
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

    /// <summary>
    /// Loads configuration while the caller holds <see cref="_gate"/>.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels document operations.</param>
    /// <returns>A manager-safe configuration result.</returns>
    /// <remarks>
    /// Missing defaults are persisted before reading. Any exception raised after entry, including
    /// cancellation from a document operation, is logged and converted to a configuration failure.
    /// </remarks>
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

    /// <inheritdoc />
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

    /// <summary>
    /// Validates, protects, and persists a complete settings update while the caller holds <see cref="_gate"/>.
    /// </summary>
    /// <param name="request">The settings update to persist.</param>
    /// <param name="cancellationToken">A token that cancels document operations.</param>
    /// <returns>The manager-safe saved configuration, or a validation or configuration failure.</returns>
    /// <remarks>
    /// Existing protected keys are retained unless a replacement is supplied or clearing is requested.
    /// Temperature, output-token, and timeout values are clamped. The submitted provider-id index is
    /// written first; the subsequent configuration load reintroduces omitted built-in profiles while
    /// omitted custom profiles remain outside the index. Setting documents for omitted profiles are not
    /// explicitly deleted.
    /// Any exception raised after entry is logged and converted to a configuration failure.
    /// </remarks>
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

                var provider = ToCoreProvider(update.Provider);
                var supportsContentEnhancement = SupportsContentEnhancement(provider);

                var profile = new AiProviderProfile(
                    update.Id.Trim(),
                    string.IsNullOrWhiteSpace(update.DisplayName) ? update.Provider.ToString() : update.DisplayName.Trim(),
                    provider,
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

    /// <inheritdoc />
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

    /// <summary>
    /// Builds the provider picker from the manager-safe configuration while the caller holds <see cref="_gate"/>.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels document operations.</param>
    /// <returns>
    /// Enabled providers that support content enhancement and have a model plus an endpoint or API key.
    /// </returns>
    private async Task<Result<IReadOnlyList<AiProviderOption>, AeroError>> GetProviderOptionsCoreAsync(
        CancellationToken cancellationToken)
    {
        var configResult = await GetConfigurationCoreAsync(cancellationToken);
        if (configResult is Result<AiSettingsConfiguration, AeroError>.Failure failure)
        {
            return failure.Error;
        }

        var config = ((Result<AiSettingsConfiguration, AeroError>.Ok)configResult).Value;
        if (!config.Enabled)
        {
            return Array.Empty<AiProviderOption>();
        }

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

    /// <inheritdoc />
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

    /// <summary>
    /// Resolves and validates invocation settings while the caller holds <see cref="_gate"/>.
    /// </summary>
    /// <param name="providerId">The requested profile identifier, or an empty value for the configured default.</param>
    /// <param name="cancellationToken">A token that cancels document operations.</param>
    /// <returns>Runtime settings containing any recovered credential, or a configuration-oriented failure.</returns>
    /// <remarks>
    /// A protected profile key takes precedence over host configuration. If no protected key exists,
    /// configuration is checked by provider kind, then profile identifier, then <c>Ai:DefaultApiKey</c>.
    /// Any exception raised after entry, including unprotect failures and cancellation during document
    /// access, is logged and converted to a configuration failure.
    /// </remarks>
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

    /// <inheritdoc />
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

    /// <summary>
    /// Adds missing built-in profiles and global settings while the caller holds <see cref="_gate"/>.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels document operations.</param>
    /// <returns>A task that completes after the session saves pending changes.</returns>
    /// <remarks>
    /// Existing profile documents are preserved. Configuration-sourced API keys are copied only when
    /// a profile is first created and are protected before serialization.
    /// </remarks>
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

    /// <summary>
    /// Loads profiles referenced by the provider-id index and orders them for display.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels document operations.</param>
    /// <returns>The loaded profiles ordered by provider kind and display name.</returns>
    /// <remarks>
    /// Missing documents fall back to matching built-in profiles. Deserialization returning
    /// <see langword="null"/> silently omits that profile; malformed JSON propagates to the caller.
    /// The content-enhancement capability is recomputed rather than trusted from stored JSON.
    /// </remarks>
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

    /// <summary>
    /// Projects a stored provider profile to the manager-safe settings contract.
    /// </summary>
    /// <param name="profile">The stored provider profile.</param>
    /// <param name="defaultProviderId">The configured default profile identifier.</param>
    /// <returns>A settings view that exposes key presence but not the protected or plaintext key.</returns>
    private static AiProviderSettings ToSettings(AiProviderProfile profile, string defaultProviderId)
        => new(
            profile.Id,
            profile.DisplayName,
            ToContractProvider(profile.Provider),
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

    /// <summary>
    /// Determines whether a manager-safe provider has the minimum model and connectivity settings.
    /// </summary>
    /// <param name="provider">The provider settings to inspect.</param>
    /// <returns>
    /// <see langword="true"/> when a model is present and either an endpoint or API key is present;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    private static bool IsConfigured(AiProviderSettings provider)
        => !string.IsNullOrWhiteSpace(provider.Model)
            && (!string.IsNullOrWhiteSpace(provider.Endpoint) || provider.HasApiKey);

    /// <summary>
    /// Determines whether this module currently exposes content operations for a provider kind.
    /// </summary>
    /// <param name="provider">The provider kind to inspect.</param>
    /// <returns><see langword="false"/> only for the reserved future-provider kind.</returns>
    private static bool SupportsContentEnhancement(CoreAiProviderKind provider)
        => provider is not CoreAiProviderKind.Future;

    private static CoreAiProviderKind ToCoreProvider(ContractAiProviderKind provider)
        => provider switch
        {
            ContractAiProviderKind.OpenAi => CoreAiProviderKind.OpenAi,
            ContractAiProviderKind.Anthropic => CoreAiProviderKind.Anthropic,
            ContractAiProviderKind.Google => CoreAiProviderKind.Google,
            ContractAiProviderKind.Groq => CoreAiProviderKind.Groq,
            ContractAiProviderKind.DeepSeek => CoreAiProviderKind.DeepSeek,
            ContractAiProviderKind.MiniMax => CoreAiProviderKind.MiniMax,
            ContractAiProviderKind.Mistral => CoreAiProviderKind.Mistral,
            ContractAiProviderKind.XAi => CoreAiProviderKind.XAi,
            ContractAiProviderKind.Zai => CoreAiProviderKind.Zai,
            ContractAiProviderKind.Perplexity => CoreAiProviderKind.Perplexity,
            ContractAiProviderKind.Alibaba => CoreAiProviderKind.Alibaba,
            ContractAiProviderKind.OpenRouter => CoreAiProviderKind.OpenRouter,
            ContractAiProviderKind.LmStudio => CoreAiProviderKind.LmStudio,
            ContractAiProviderKind.OpenCode => CoreAiProviderKind.OpenCode,
            ContractAiProviderKind.Future => CoreAiProviderKind.Future,
            _ => CoreAiProviderKind.Future
        };

    private static ContractAiProviderKind ToContractProvider(CoreAiProviderKind provider)
        => provider switch
        {
            CoreAiProviderKind.OpenAi => ContractAiProviderKind.OpenAi,
            CoreAiProviderKind.Anthropic => ContractAiProviderKind.Anthropic,
            CoreAiProviderKind.Google => ContractAiProviderKind.Google,
            CoreAiProviderKind.Groq => ContractAiProviderKind.Groq,
            CoreAiProviderKind.DeepSeek => ContractAiProviderKind.DeepSeek,
            CoreAiProviderKind.MiniMax => ContractAiProviderKind.MiniMax,
            CoreAiProviderKind.Mistral => ContractAiProviderKind.Mistral,
            CoreAiProviderKind.XAi => ContractAiProviderKind.XAi,
            CoreAiProviderKind.Zai => ContractAiProviderKind.Zai,
            CoreAiProviderKind.Perplexity => ContractAiProviderKind.Perplexity,
            CoreAiProviderKind.Alibaba => ContractAiProviderKind.Alibaba,
            CoreAiProviderKind.OpenRouter => ContractAiProviderKind.OpenRouter,
            CoreAiProviderKind.LmStudio => ContractAiProviderKind.LmStudio,
            CoreAiProviderKind.OpenCode => ContractAiProviderKind.OpenCode,
            CoreAiProviderKind.Future => ContractAiProviderKind.Future,
            _ => ContractAiProviderKind.Future
        };

    /// <summary>
    /// Serializes and stages a provider profile in the current document session.
    /// </summary>
    /// <param name="profile">The profile to stage.</param>
    /// <remarks>The method does not commit the session; the calling operation controls persistence.</remarks>
    private void StoreProfile(AiProviderProfile profile)
    {
        StoreSetting(ProfileKey(profile.Id), JsonSerializer.Serialize(profile, JsonOptions), "json");
    }

    /// <summary>
    /// Stages a setting document with the AI category and a fresh modification timestamp.
    /// </summary>
    /// <param name="key">The document key.</param>
    /// <param name="value">The serialized setting value.</param>
    /// <param name="type">The descriptive value type stored with the setting.</param>
    /// <remarks>The method does not commit the session; the calling operation controls persistence.</remarks>
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

    /// <summary>
    /// Loads and normalizes a string setting.
    /// </summary>
    /// <param name="key">The setting key to load.</param>
    /// <param name="fallback">The value used when the setting is missing or white space.</param>
    /// <param name="cancellationToken">A token that cancels document access.</param>
    /// <returns>The trimmed stored value, or <paramref name="fallback"/>.</returns>
    private async Task<string?> GetStringSettingAsync(string key, string? fallback, CancellationToken cancellationToken)
    {
        var setting = await session.LoadAsync<Setting>(key, cancellationToken);
        return string.IsNullOrWhiteSpace(setting?.Value) ? fallback : setting.Value.Trim();
    }

    /// <summary>
    /// Loads a Boolean setting with a fallback for missing or unparseable values.
    /// </summary>
    /// <param name="key">The setting key to load.</param>
    /// <param name="fallback">The value returned when parsing does not succeed.</param>
    /// <param name="cancellationToken">A token that cancels document access.</param>
    /// <returns>The parsed value, or <paramref name="fallback"/>.</returns>
    private async Task<bool> GetBoolSettingAsync(string key, bool fallback, CancellationToken cancellationToken)
    {
        var value = await GetStringSettingAsync(key, null, cancellationToken);
        return bool.TryParse(value, out var parsed) ? parsed : fallback;
    }

    /// <summary>
    /// Resolves an API key from host configuration for a provider profile.
    /// </summary>
    /// <param name="profile">The provider profile used to construct lookup paths.</param>
    /// <returns>
    /// The first configured value found by provider kind, profile identifier, or the default-key path;
    /// otherwise, <see langword="null"/>.
    /// </returns>
    /// <remarks>The returned plaintext value is sensitive and is not normalized or logged here.</remarks>
    private string? GetConfiguredApiKey(AiProviderProfile profile)
    {
        var providerName = profile.Provider.ToString();
        return configuration[$"Ai:Providers:{providerName}:ApiKey"]
            ?? configuration[$"Ai:Providers:{profile.Id}:ApiKey"]
            ?? configuration["Ai:DefaultApiKey"];
    }

    /// <summary>
    /// Parses a Boolean host-configuration value.
    /// </summary>
    /// <param name="key">The configuration path.</param>
    /// <param name="fallback">The value returned when the path is missing or unparseable.</param>
    /// <returns>The parsed value, or <paramref name="fallback"/>.</returns>
    private bool GetBoolConfiguration(string key, bool fallback)
        => bool.TryParse(configuration[key], out var value) ? value : fallback;

    /// <summary>
    /// Parses, trims, and case-insensitively de-duplicates the provider-id index.
    /// </summary>
    /// <param name="raw">The persisted JSON array, if present.</param>
    /// <param name="defaults">Profiles whose identifiers supply the fallback index.</param>
    /// <returns>The parsed identifiers, or the built-in identifiers when input is empty, malformed, or deserializes to null.</returns>
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

    /// <summary>
    /// Constructs the setting-document key for a provider profile.
    /// </summary>
    /// <param name="providerId">The profile identifier.</param>
    /// <returns>The provider prefix followed by <paramref name="providerId"/>.</returns>
    private static string ProfileKey(string providerId)
        => $"{AiSettingKeys.ProviderPrefix}{providerId}";

    /// <summary>
    /// Converts empty input to <see langword="null"/> and trims non-empty input.
    /// </summary>
    /// <param name="value">The value to normalize.</param>
    /// <returns>A trimmed value, or <see langword="null"/> for null, empty, or white-space input.</returns>
    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
