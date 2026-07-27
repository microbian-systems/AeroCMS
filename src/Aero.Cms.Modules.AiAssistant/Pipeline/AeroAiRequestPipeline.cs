using System.Diagnostics;
using Aero.Cms.Abstractions.Ai.Assistant;
using Aero.Cms.Abstractions.Ai.Pipeline;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.AiAssistant.Pipeline;

/// <summary>Composes the applicable stages in deterministic order for every invocation.</summary>
public sealed class AeroAiRequestPipeline(IEnumerable<IAeroAiPipelineStage> stages)
    : IAeroAiRequestPipeline
{
    private readonly IReadOnlyList<IAeroAiPipelineStage> _stages = stages
        .OrderBy(stage => stage.Order)
        .ThenBy(stage => stage.Name, StringComparer.Ordinal)
        .ToArray();

    public Task<Result<T>> ExecuteAsync<T>(
        AeroAiPipelineContext context,
        AeroAiPipelineNext<T> terminal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(terminal);

        AeroAiPipelineNext<T> next = terminal;
        foreach (var stage in _stages
                     .Where(stage => stage.AppliesTo(context))
                     .Reverse())
        {
            var capturedStage = stage;
            var capturedNext = next;
            next = (current, ct) =>
                capturedStage.InvokeAsync(current, capturedNext, ct);
        }

        return next(context, cancellationToken);
    }
}

/// <summary>Fails closed when normalized request metadata is absent or outside protocol bounds.</summary>
public sealed class AeroAiRequestNormalizationStage : IAeroAiPipelineStage
{
    public string Name => "request-normalization";

    public int Order => AeroAiPipelineOrder.RequestNormalization;

    public bool AppliesTo(AeroAiPipelineContext context) => true;

    public Task<Result<T>> InvokeAsync<T>(
        AeroAiPipelineContext context,
        AeroAiPipelineNext<T> next,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(context.Audience) ||
            !Enum.IsDefined(context.Operation) ||
            string.IsNullOrWhiteSpace(context.CorrelationId) ||
            context.CorrelationId.Length > 128 ||
            string.IsNullOrWhiteSpace(context.Culture) ||
            context.InputItemCount < 0 ||
            context.InputCharacterCount < 0)
        {
            return Task.FromResult<Result<T>>(
                AeroError.InvalidRequestError("AI request metadata was invalid."));
        }

        return next(context, cancellationToken);
    }
}

/// <summary>Rechecks the server-derived identity, tenant, and site scope before provider access.</summary>
public sealed class AeroAiScopeStage : IAeroAiPipelineStage
{
    public string Name => "scope";

    public int Order => AeroAiPipelineOrder.Scope;

    public bool AppliesTo(AeroAiPipelineContext context) =>
        context.Audience is AeroAiAudience.Manager or AeroAiAudience.Member or AeroAiAudience.Mcp;

    public Task<Result<T>> InvokeAsync<T>(
        AeroAiPipelineContext context,
        AeroAiPipelineNext<T> next,
        CancellationToken cancellationToken)
    {
        if (context.Principal.Identity?.IsAuthenticated != true)
        {
            return Task.FromResult<Result<T>>(
                AeroError.UnauthorizedError("Authentication is required."));
        }

        if (context.PrincipalId <= 0 || context.TenantId <= 0 || context.SiteId <= 0)
        {
            return Task.FromResult<Result<T>>(
                AeroError.ForbiddenError("A valid principal, tenant, and site scope is required."));
        }

        return next(context, cancellationToken);
    }
}

/// <summary>Applies protocol-level input bounds before conversation or provider work is loaded.</summary>
public sealed class AeroAiInputSafetyStage : IAeroAiPipelineStage
{
    public string Name => "input-safety";

    public int Order => AeroAiPipelineOrder.InputSafety;

    public bool AppliesTo(AeroAiPipelineContext context) =>
        context.Operation == AeroAiOperation.Assistant;

    public Task<Result<T>> InvokeAsync<T>(
        AeroAiPipelineContext context,
        AeroAiPipelineNext<T> next,
        CancellationToken cancellationToken)
    {
        if (context.InputItemCount > AeroCmsAssistantLimits.MaxMessages ||
            context.InputCharacterCount > AeroCmsAssistantLimits.MaxConversationCharacters)
        {
            return Task.FromResult<Result<T>>(
                AeroError.ValidationError(["AI request input exceeded the allowed bounds."]));
        }

        return next(context, cancellationToken);
    }
}

/// <summary>Records safe stage outcome metadata without prompts, documents, or tool payloads.</summary>
public sealed class AeroAiTelemetryStage(ILogger<AeroAiTelemetryStage> logger)
    : IAeroAiPipelineStage
{
    public string Name => "persistence-and-telemetry";

    public int Order => AeroAiPipelineOrder.PersistenceAndTelemetry;

    public bool AppliesTo(AeroAiPipelineContext context) => true;

    public async Task<Result<T>> InvokeAsync<T>(
        AeroAiPipelineContext context,
        AeroAiPipelineNext<T> next,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            var result = await next(context, cancellationToken);
            logger.LogInformation(
                "AI pipeline completed. Audience={Audience} Operation={Operation} TenantId={TenantId} SiteId={SiteId} Streaming={Streaming} Succeeded={Succeeded} ElapsedMs={ElapsedMs} CorrelationId={CorrelationId}",
                context.Audience,
                context.Operation,
                context.TenantId,
                context.SiteId,
                context.IsStreaming,
                result is Result<T>.Ok,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                context.CorrelationId);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation(
                "AI pipeline cancelled. Audience={Audience} Operation={Operation} TenantId={TenantId} SiteId={SiteId} CorrelationId={CorrelationId}",
                context.Audience,
                context.Operation,
                context.TenantId,
                context.SiteId,
                context.CorrelationId);
            return AeroError.CancelledError("AI request was cancelled.");
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "AI pipeline failed. Audience={Audience} Operation={Operation} TenantId={TenantId} SiteId={SiteId} CorrelationId={CorrelationId}",
                context.Audience,
                context.Operation,
                context.TenantId,
                context.SiteId,
                context.CorrelationId);
            return AeroError.CreateError("AI request pipeline failed.");
        }
    }
}
