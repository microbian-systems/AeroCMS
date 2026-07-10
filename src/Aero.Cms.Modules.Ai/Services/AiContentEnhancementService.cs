using Aero.Cms.Abstractions.Ai;
using Aero.Cms.Modules.Ai.Configuration;
using Aero.Core;
using Aero.Core.Ai;
using Aero.Core.Extensions;
using Aero.Core.Railway;
using FluentValidation;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Aero.Cms.Modules.Ai.Services;

/// <summary>
/// Represents a class for AiContentEnhancementService.
/// </summary>
public sealed class AiContentEnhancementService(
    IAiSettingsProvider settingsProvider,
    IAiChatClientFactory chatClientFactory,
    IEnhanceContentPromptBuilder promptBuilder,
    IValidator<EnhanceContentRequest> validator,
    ILogger<AiContentEnhancementService> log) : IAiContentEnhancementService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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

        /// <summary>
    /// EnhanceAsync method.
    /// </summary>
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
            var json = request.ToJson();
            log.LogError($"ai settings error: {settingsFailure.Error}");
            log.LogDebug($"request: {json}");

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
}
