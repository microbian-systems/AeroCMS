using Aero.Cms.Abstractions.Ai;
using Aero.Cms.Modules.Ai.Configuration;
using Aero.Core;
using Aero.Core.Ai;
using Aero.Core.Extensions;
using Aero.Core.Railway;
using FluentValidation;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace Aero.Cms.Modules.Ai.Services;

/// <summary>
/// Validates CMS field content, invokes the selected chat provider, and parses a structured enhancement.
/// </summary>
/// <param name="settingsProvider">Resolves the selected provider's runtime settings and credential.</param>
/// <param name="chatClientFactory">Creates a provider-specific chat client.</param>
/// <param name="promptBuilder">Serializes the request into the user message.</param>
/// <param name="validator">Validates request shape and size before provider access.</param>
/// <param name="log">Records configuration, parsing, truncation, and invocation failures.</param>
/// <remarks>
/// Request content is placed in a user-role prompt and can be sent to an external provider. Prompt text
/// is not treated as sanitized or trusted. The current implementation performs a single buffered
/// <c>GetResponseAsync</c> call even when runtime settings request streaming, returns no usage object,
/// and does not persist usage telemetry.
/// </remarks>
public sealed class AiContentEnhancementService(
    IAiSettingsProvider settingsProvider,
    IAiChatClientFactory chatClientFactory,
    IEnhanceContentPromptBuilder promptBuilder,
    IValidator<EnhanceContentRequest> validator,
    ILogger<AiContentEnhancementService> log) : IAiContentEnhancementService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const int MaxStreamOutputCharacters = 1_000_000;

    private const string Instructions = """
        You are a sr. staff technical sales engineer / technologist and teacher and author/writer. 
        You excel at introducing technical concepts to non-technical potential users/prospects for 
        using new technologies. You explain things use the \"explain it like i'm five\" (ELI5) method 
        and are very friendly in your writing while still getting the technical concepts across.
        Improve the supplied CMS content according to the user's prompt.
        Preserve the original meaning, factual claims, markdown structure, links, code blocks, 
        and front matter unless the user explicitly asks to change them.
        Do not invent facts, quotes, statistics, sources, product claims, or dates.
        Return only structured output matching the requested schema.
        If the request is unsafe, ambiguous, or would require inventing facts, return a warning and 
        keep the text conservative. No cussing. No questionable material responses.
        """;

    /// <inheritdoc />
    /// <remarks>
    /// The operation applies a linked timeout clamped to 1–300 seconds. Both caller cancellation and
    /// timeout cancellation observed during the provider call are returned as an AI timeout failure.
    /// Settings-resolution diagnostics omit request content, metadata, and user instructions. Provider or parse failures do not persist content.
    /// Provider output is accepted only after structured parsing, but this does not establish factuality or safety.
    /// </remarks>
public async Task<Result<EnhanceContentResponse>> EnhanceAsync(
        EnhanceContentRequest request,
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
            log.LogError($"ai settings error: {settingsFailure.Error}");

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
            // Build chat messages manually: system instructions + user prompt
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

            // Detect token-limit truncation via the API's finish reason
            if (chatResponse.FinishReason == ChatFinishReason.Length)
            {
                var outputTokens = chatResponse.Usage?.OutputTokenCount;
                log.LogWarning(
                    "AI response truncated: FinishReason=Length, MaxOutputTokens={MaxTokens}, OutputTokens={OutputTokens}",
                    settings.MaxOutputTokens, outputTokens);
                return AeroError.CreateError(
                    $"AI response was truncated because it hit the output token limit ({settings.MaxOutputTokens}). " +
                    $"Increase the Max Output Tokens in AI Settings and try again.");
            }

            if (string.IsNullOrWhiteSpace(rawText))
            {
                return AeroError.CreateError("AI provider returned an empty response.");
            }

            EnhanceContentAgentOutput? output;
            try
            {
                output = EnhanceContentAgentOutputParser.Deserialize(rawText, JsonOptions);
            }
            catch (JsonException ex) when (ex.Message.Contains("end of data", StringComparison.OrdinalIgnoreCase))
            {
                log.LogError(ex, "AI provider response was truncated — likely MaxOutputTokens too low.");
                return AeroError.CreateError(
                    $"AI response was truncated. Try increasing the Max Output Tokens setting for this provider (current: {settings.MaxOutputTokens}) and try again.");
            }
            catch (JsonException ex)
            {
                log.LogError(ex, "Error deserializing AI provider response.");
                return AeroError.CreateError("AI provider returned an unparseable response. Try increasing MaxOutputTokens if the content is large.");
            }

            if (output is null)
            {
                return AeroError.CreateError("AI provider returned an unparseable response.");
            }
            if (string.IsNullOrWhiteSpace(output.EnhancedText))
            {
                return AeroError.CreateError("AI provider returned an empty enhancement.");
            }

            var result = new EnhanceContentResponse(
                output.EnhancedText,
                output.Rationale,
                output.Warnings ?? [],
                settings.DisplayName,
                settings.Model ?? string.Empty,
                Usage: null);

            return result;
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            return AeroError.TimeoutError("AI enhancement timed out.");
        }
        catch (Exception ex)
        {
            log.LogError(
                ex,
                "AI content enhancement failed for provider {Provider}, model {Model}, target {TargetField}.",
                settings.Provider,
                settings.Model,
                request.TargetField);

            return AeroError.CreateError("AI enhancement failed: " + ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<Result<IAsyncEnumerable<EnhanceContentEvent>>> StreamAsync(
        EnhanceContentRequest request,
        string correlationId,
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
            log.LogWarning(
                "AI enhancement stream settings resolution failed. CorrelationId={CorrelationId}",
                correlationId);
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

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, Instructions),
            new(ChatRole.User, promptBuilder.Build(request))
        };
        var options = new ChatOptions
        {
            Temperature = settings.Temperature,
            MaxOutputTokens = settings.MaxOutputTokens
        };

        return new Result<IAsyncEnumerable<EnhanceContentEvent>>.Ok(
            StreamCoreAsync(
                ((Result<IChatClient>.Ok)clientResult).Value,
                settings,
                messages,
                options,
                correlationId,
                request.TargetField,
                cancellationToken));
    }

    private async IAsyncEnumerable<EnhanceContentEvent> StreamCoreAsync(
        IChatClient client,
        AiRuntimeSettings settings,
        IReadOnlyList<ChatMessage> messages,
        ChatOptions options,
        string correlationId,
        string targetField,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using (client)
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(settings.TimeoutSeconds, 1, 300)));
            var rawOutput = new StringBuilder();
            var enhancedText = new StreamingJsonStringProjector("enhancedText");
            EnhanceContentEvent? terminalError = null;

            yield return new(
                EnhanceContentEventKind.Metadata,
                CorrelationId: correlationId,
                Provider: settings.DisplayName,
                Model: settings.Model);

            await using var enumerator = client.GetStreamingResponseAsync(
                    messages,
                    options,
                    timeout.Token)
                .GetAsyncEnumerator(timeout.Token);

            while (terminalError is null)
            {
                bool hasNext;
                try
                {
                    hasNext = await enumerator.MoveNextAsync();
                }
                catch (OperationCanceledException) when (timeout.IsCancellationRequested)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        terminalError = new(
                            EnhanceContentEventKind.Error,
                            "AI enhancement timed out.",
                            CorrelationId: correlationId);
                    }

                    break;
                }
                catch (Exception ex)
                {
                    log.LogWarning(
                        ex,
                        "AI enhancement streaming failed. CorrelationId={CorrelationId} Provider={Provider} Model={Model} Target={TargetField}",
                        correlationId,
                        settings.Provider,
                        settings.Model,
                        targetField);
                    terminalError = new(
                        EnhanceContentEventKind.Error,
                        "AI enhancement provider invocation failed.",
                        CorrelationId: correlationId);
                    break;
                }

                if (!hasNext)
                {
                    break;
                }

                var providerDelta = enumerator.Current.Text;
                if (string.IsNullOrEmpty(providerDelta))
                {
                    continue;
                }

                if (rawOutput.Length + providerDelta.Length > MaxStreamOutputCharacters)
                {
                    terminalError = new(
                        EnhanceContentEventKind.Error,
                        "AI enhancement exceeded the allowed output size.",
                        CorrelationId: correlationId);
                    break;
                }

                rawOutput.Append(providerDelta);
                var previewDelta = enhancedText.Append(providerDelta);
                for (var offset = 0; offset < previewDelta.Length; offset += 8_000)
                {
                    var length = Math.Min(8_000, previewDelta.Length - offset);
                    yield return new(
                        EnhanceContentEventKind.Delta,
                        previewDelta.Substring(offset, length),
                        CorrelationId: correlationId);
                }
            }

            if (terminalError is not null)
            {
                yield return terminalError;
                yield break;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                yield break;
            }

            if (rawOutput.Length == 0)
            {
                yield return new(
                    EnhanceContentEventKind.Error,
                    "AI provider returned an empty response.",
                    CorrelationId: correlationId);
                yield break;
            }

            EnhanceContentAgentOutput? output;
            string? parseError = null;
            try
            {
                output = EnhanceContentAgentOutputParser.Deserialize(rawOutput.ToString(), JsonOptions);
            }
            catch (JsonException ex)
            {
                log.LogWarning(
                    ex,
                    "AI enhancement stream returned unparseable output. CorrelationId={CorrelationId}",
                    correlationId);
                output = null;
                parseError = "AI provider returned an unparseable response.";
            }

            if (parseError is not null)
            {
                yield return new(
                    EnhanceContentEventKind.Error,
                    parseError,
                    CorrelationId: correlationId);
                yield break;
            }

            if (output is null || string.IsNullOrWhiteSpace(output.EnhancedText))
            {
                yield return new(
                    EnhanceContentEventKind.Error,
                    "AI provider returned an empty enhancement.",
                    CorrelationId: correlationId);
                yield break;
            }

            var response = new EnhanceContentResponse(
                output.EnhancedText,
                output.Rationale,
                output.Warnings ?? [],
                settings.DisplayName,
                settings.Model ?? string.Empty,
                Usage: null);

            yield return new(
                EnhanceContentEventKind.Complete,
                Response: response,
                CorrelationId: correlationId,
                Provider: settings.DisplayName,
                Model: settings.Model);
        }
    }
}
