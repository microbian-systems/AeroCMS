using Aero.Cms.Abstractions.Ai;
using Aero.Core;
using Aero.Core.Railway;
using LlmTornado;
using LlmTornado.Chat.Models;
using LlmTornado.Microsoft.Extensions.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Ai.Configuration;

public sealed class TornadoAiChatClientFactory(
    IAiSettingsProvider settingsProvider,
    ILogger<TornadoAiChatClientFactory> logger) : IAiChatClientFactory
{
    public async Task<Result<IChatClient, AeroError>> CreateAsync(CancellationToken cancellationToken = default)
    {
        var settingsResult = await settingsProvider.GetAsync(cancellationToken);
        if (settingsResult is Result<AiSettings, AeroError>.Failure settingsFailure)
        {
            return settingsFailure.Error;
        }

        var settings = ((Result<AiSettings, AeroError>.Ok)settingsResult).Value;
        if (!settings.Enabled)
        {
            return AeroError.ConfigurationError("AI is disabled.");
        }

        if (settings.Provider == AiProviderKind.OpenCode)
        {
            return AeroError.ConfigurationError("OpenCode AI provider support is planned but is not implemented yet.");
        }

        if (string.IsNullOrWhiteSpace(settings.Model))
        {
            return AeroError.ConfigurationError("AI model is not configured.");
        }

        try
        {
            var api = CreateTornadoApi(settings);
            IChatClient client = api.AsChatClient(new ChatModel(settings.Model));
            return new Result<IChatClient, AeroError>.Ok(client);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create AI chat client for provider {Provider}.", settings.Provider);
            return AeroError.ConfigurationError("AI provider could not be initialized.");
        }
    }

    private static TornadoApi CreateTornadoApi(AiSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.Endpoint))
        {
            return new TornadoApi(new Uri(settings.Endpoint, UriKind.Absolute));
        }

        var apiKey = ResolveApiKey(settings);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("AI API key is not configured.");
        }

        return new TornadoApi(apiKey);
    }

    private static string? ResolveApiKey(AiSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.ApiKeyEnvironmentVariable))
        {
            return Environment.GetEnvironmentVariable(settings.ApiKeyEnvironmentVariable);
        }

        if (!string.IsNullOrWhiteSpace(settings.ApiKeySecretName))
        {
            throw new InvalidOperationException("Secrets-backed AI API keys are not wired yet.");
        }

        return null;
    }
}
