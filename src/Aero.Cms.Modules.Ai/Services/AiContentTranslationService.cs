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

/// <summary>
/// Validates CMS fields, invokes the selected chat provider, and maps structured translations back to input keys.
/// </summary>
/// <param name="settingsProvider">Resolves the selected provider's runtime settings and credential.</param>
/// <param name="chatClientFactory">Creates a provider-specific chat client.</param>
/// <param name="promptBuilder">Serializes cultures and fields into the user message.</param>
/// <param name="validator">Validates request shape and size before provider access.</param>
/// <param name="log">Records configuration, parsing, truncation, and invocation failures.</param>
/// <remarks>
/// Source fields are placed in a user-role prompt and can be sent to an external provider. Source text is
/// not treated as sanitized or trusted. The current implementation performs a single buffered
/// <c>GetResponseAsync</c> call even when runtime settings request streaming and does not persist usage telemetry.
/// </remarks>
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

    /// <summary>
    /// Validates and submits document fields to an AI provider for translation.
    /// </summary>
    /// <param name="request">The source and target cultures, fields, and optional provider selection.</param>
    /// <param name="cancellationToken">A token that requests cancellation of validation, settings lookup, and provider access.</param>
    /// <returns>
    /// A successful response keyed by the original field identifiers, or a failure describing validation,
    /// configuration, provider-client creation, timeout, empty output, truncation, parsing, or invocation failure.
    /// </returns>
    /// <remarks>
    /// The operation applies a linked timeout clamped to 1–300 seconds. Both caller cancellation and
    /// timeout cancellation observed during the provider call are returned as an AI timeout failure.
    /// If settings resolution fails, only the provider configuration error is logged. Missing output
    /// fields retain their source text and add a warning.
    /// Returned values remain provider-generated and are not independently verified by this service.
    /// </remarks>
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
                settings.ProviderId,
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
