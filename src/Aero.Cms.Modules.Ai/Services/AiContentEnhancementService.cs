using Aero.Cms.Abstractions.Ai;
using Aero.Cms.Modules.Ai.Configuration;
using Aero.Core;
using Aero.Core.Railway;
using FluentValidation;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Ai.Services;

public sealed class AiContentEnhancementService(
    IAiSettingsProvider settingsProvider,
    IAiChatClientFactory chatClientFactory,
    IEnhanceContentPromptBuilder promptBuilder,
    IValidator<EnhanceContentRequest> validator,
    ILogger<AiContentEnhancementService> logger) : IAiContentEnhancementService
{
    private const string Instructions = """
        You are an editorial assistant inside AeroCMS.
        Improve the supplied CMS content according to the user's prompt.
        Preserve the original meaning, factual claims, markdown structure, links, code blocks, 
        and front matter unless the user explicitly asks to change them.
        Do not invent facts, quotes, statistics, sources, product claims, or dates.
        Return only structured output matching the requested schema.
        If the request is unsafe, ambiguous, or would require inventing facts, return a warning and 
        keep the text conservative. No cussing. No questionable material responses.
        """;

    public async Task<Result<EnhanceContentResponse, AeroError>> EnhanceAsync(
        EnhanceContentRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return AeroError.ValidationError(validation.Errors.Select(error => error.ErrorMessage));
        }

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

        var clientResult = await chatClientFactory.CreateAsync(cancellationToken);
        if (clientResult is Result<IChatClient, AeroError>.Failure clientFailure)
        {
            return clientFailure.Error;
        }

        var client = ((Result<IChatClient, AeroError>.Ok)clientResult).Value;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(settings.TimeoutSeconds, 1, 300)));

        try
        {
            var agent = new ChatClientAgent(client, instructions: Instructions);
            var chatOptions = new ChatOptions
            {
                Temperature = settings.Temperature,
                MaxOutputTokens = settings.MaxOutputTokens
            };

            var response = await agent.RunAsync<EnhanceContentAgentOutput>(
                promptBuilder.Build(request),
                session: null,
                serializerOptions: new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web),
                options: new ChatClientAgentRunOptions(chatOptions),
                cancellationToken: timeout.Token);

            var output = response.Result;
            if (string.IsNullOrWhiteSpace(output.EnhancedText))
            {
                return AeroError.CreateError("AI provider returned an empty enhancement.");
            }

            var result = new EnhanceContentResponse(
                output.EnhancedText,
                output.Rationale,
                output.Warnings ?? [],
                settings.Provider.ToString(),
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
