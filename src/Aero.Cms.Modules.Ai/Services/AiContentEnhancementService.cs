using Aero.Cms.Abstractions.Ai;
using Aero.Cms.Modules.Ai.Configuration;
using Aero.Core;
using Aero.Core.Railway;
using FluentValidation;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Aero.Cms.Modules.Ai.Services;

public sealed class AiContentEnhancementService(
    IAiSettingsProvider settingsProvider,
    IAiChatClientFactory chatClientFactory,
    IEnhanceContentPromptBuilder promptBuilder,
    IValidator<EnhanceContentRequest> validator,
    ILogger<AiContentEnhancementService> logger) : IAiContentEnhancementService
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
    /// Strips markdown code fences (```json ... ```) and surrounding whitespace from LLM responses
    /// that wrap JSON in Markdown formatting despite instructions.
    /// </summary>
    private static string StripMarkdownFences(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            // Remove opening fence (e.g. "```json" or "```")
            var firstNewline = trimmed.IndexOf('\n');
            if (firstNewline > 0)
            {
                trimmed = trimmed[(firstNewline + 1)..];
            }

            // Remove closing fence if present
            var closingFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (closingFence >= 0)
            {
                trimmed = trimmed[..closingFence];
            }

            trimmed = trimmed.Trim();
        }

        return trimmed;
    }

    public async Task<Result<EnhanceContentResponse, AeroError>> EnhanceAsync(
        EnhanceContentRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return AeroError.ValidationError(validation.Errors.Select(error => error.ErrorMessage));
        }

        var settingsResult = await settingsProvider.GetAsync(request.ProviderId, cancellationToken);
        if (settingsResult is Result<AiRuntimeSettings, AeroError>.Failure settingsFailure)
        {
            return settingsFailure.Error;
        }

        var settings = ((Result<AiRuntimeSettings, AeroError>.Ok)settingsResult).Value;
        if (!settings.Enabled)
        {
            return AeroError.ConfigurationError("AI is disabled.");
        }

        var clientResult = await chatClientFactory.CreateAsync(settings, cancellationToken);
        if (clientResult is Result<IChatClient, AeroError>.Failure clientFailure)
        {
            return clientFailure.Error;
        }

        var client = ((Result<IChatClient, AeroError>.Ok)clientResult).Value;
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

            if (string.IsNullOrWhiteSpace(rawText))
            {
                return AeroError.CreateError("AI provider returned an empty response.");
            }

            // Some LLMs wrap JSON in markdown code fences — strip them before deserializing
            var cleaned = StripMarkdownFences(rawText);

            EnhanceContentAgentOutput? output;
            try
            {
                output = JsonSerializer.Deserialize<EnhanceContentAgentOutput>(cleaned, JsonOptions);
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "Error deserializing AI provider response.");
                // If that fails, try parsing from the raw text as a fallback
                output = JsonSerializer.Deserialize<EnhanceContentAgentOutput>(rawText, JsonOptions);
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
            logger.LogError(
                ex,
                "AI content enhancement failed for provider {Provider}, model {Model}, target {TargetField}.",
                settings.Provider,
                settings.Model,
                request.TargetField);

            return AeroError.CreateError("AI enhancement failed. Check provider configuration and try again.");
        }
    }

    private sealed record EnhanceContentAgentOutput(
        string EnhancedText,
        string? Rationale,
        IReadOnlyList<string>? Warnings);
}
