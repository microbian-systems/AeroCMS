using System.Text.Json;
using Aero.Cms.Abstractions.Ai;
using Aero.Cms.Modules.Ai.Configuration;
using Aero.Core;
using Aero.Core.Ai;
using Aero.Core.Extensions;
using Aero.Core.Railway;
using FluentValidation;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Ai.Services;

public sealed class AiContentTranslationService(
    IAiSettingsProvider settingsProvider,
    IAiChatClientFactory chatClientFactory,
    ITranslateDocumentPromptBuilder promptBuilder,
    IValidator<TranslateDocumentRequest> validator,
    ILogger<AiContentTranslationService> log) : IAiContentTranslationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private const string Instructions = """
        You are a professional website localization translator.
        Translate CMS content from the source culture to the target culture.
        Preserve all supplied field keys exactly.
        Preserve markdown, HTML tags, links, code blocks, and front matter.
        Do not translate URLs, code, IDs, CSS classes, or brand names.
        Return only structured JSON matching the requested schema.
        """;

    public async Task<Result<TranslateDocumentResponse>> TranslateAsync(
        TranslateDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return AeroError.ValidationError(validation.Errors.Select(error => error.ErrorMessage));
        }

        var settingsResult = await settingsProvider.GetAsync(request.ProviderId, cancellationToken);
        if (settingsResult is Result<AiRuntimeSettings>.Failure settingsFailure)
        {
            log.LogError("AI settings error while translating document: {Error}", settingsFailure.Error);
            log.LogDebug("Translation request: {Request}", request.ToJson());
            return settingsFailure.Error;
        }

        var settings = ((Result<AiRuntimeSettings>.Ok)settingsResult).Value;
        if (!settings.Enabled)
        {
            return AeroError.ConfigurationError("AI is disabled.");
        }

        var clientResult = await chatClientFactory.CreateAsync(settings, cancellationToken);
        if (clientResult is Result<IChatClient>.Failure clientFailure)
        {
            return clientFailure.Error;
        }

        var client = ((Result<IChatClient>.Ok)clientResult).Value;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(settings.TimeoutSeconds, 1, 300)));

        try
        {
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, Instructions),
                new(ChatRole.User, promptBuilder.Build(request))
            };

            var chatOptions = new ChatOptions
            {
                Temperature = settings.Temperature,
                MaxOutputTokens = settings.MaxOutputTokens
            };

            var chatResponse = await client.GetResponseAsync(messages, chatOptions, cancellationToken: timeout.Token);
            var rawText = chatResponse.Messages?.LastOrDefault()?.Text;

            if (chatResponse.FinishReason == ChatFinishReason.Length)
            {
                log.LogWarning(
                    "AI translation response truncated: MaxOutputTokens={MaxTokens}, OutputTokens={OutputTokens}",
                    settings.MaxOutputTokens,
                    chatResponse.Usage?.OutputTokenCount);

                return AeroError.CreateError(
                    $"AI translation response was truncated because it hit the output token limit ({settings.MaxOutputTokens}). " +
                    "Increase Max Output Tokens in AI Settings and try again.");
            }

            if (string.IsNullOrWhiteSpace(rawText))
            {
                return AeroError.CreateError("AI provider returned an empty translation response.");
            }

            TranslateDocumentAgentOutput? output;
            try
            {
                output = TranslateDocumentAgentOutputParser.Deserialize(rawText, JsonOptions);
            }
            catch (JsonException ex)
            {
                log.LogError(ex, "Error deserializing AI translation response.");
                return AeroError.CreateError("AI provider returned an unparseable translation response.");
            }

            if (output?.Fields is null || output.Fields.Count == 0)
            {
                return AeroError.CreateError("AI provider returned no translated fields.");
            }

            var warnings = output.Warnings?.ToList() ?? [];
            var translated = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var field in request.Fields)
            {
                if (output.Fields.TryGetValue(field.Key, out var value))
                {
                    translated[field.Key] = value ?? string.Empty;
                    continue;
                }

                translated[field.Key] = field.SourceText;
                warnings.Add($"AI response omitted field '{field.Key}', so the source text was preserved.");
            }

            return new TranslateDocumentResponse(
                translated,
                warnings,
                settings.DisplayName,
                settings.Model ?? string.Empty);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            return AeroError.TimeoutError("AI translation timed out.");
        }
        catch (Exception ex)
        {
            log.LogError(
                ex,
                "AI content translation failed for provider {Provider}, model {Model}, target culture {TargetCulture}.",
                settings.Provider,
                settings.Model,
                request.TargetCulture);

            return AeroError.CreateError("AI translation failed: " + ex.Message);
        }
    }
}
