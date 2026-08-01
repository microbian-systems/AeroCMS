using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Globalization;
using Aero.Cms.Abstractions.Ai.Assistant;
using Aero.Cms.Abstractions.Ai.Memory;
using Aero.Cms.Abstractions.Ai.Pipeline;
using Aero.Cms.Abstractions.Security;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Aero.Cms.Modules.RateLimiting;

namespace Aero.Cms.Modules.Mcp;

/// <summary>Maps the authenticated REST and SSE manager assistant boundary.</summary>
public static class AeroCmsAssistantEndpoints
{
    private const string CorrelationHeader = "X-Correlation-Id";

    public static IEndpointRouteBuilder MapAeroCmsAssistantEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/admin/mcp/assistant")
            .RequireAuthorization()
            .RequireAuthorization("site:read")
            .RequireRateLimiting(AeroRateLimitPolicyNames.AiManager);

        group.MapPost("/complete", CompleteAsync);
        group.MapPost("/stream", StreamAsync)
            .RequireRateLimiting(AeroRateLimitPolicyNames.AiStream);
        group.MapGet("/conversations", ListConversationsAsync);
        group.MapGet("/conversations/{conversationId:long}", GetConversationAsync);
        group.MapDelete("/conversations/{conversationId:long}", DeleteConversationAsync);
        group.MapGet("/memories", ListMemoriesAsync);
        group.MapPost("/memories", SaveMemoryAsync);
        group.MapPut("/memories/{memoryId:long}", UpdateMemoryAsync);
        group.MapDelete("/memories/{memoryId:long}", DeleteMemoryAsync);
        return endpoints;
    }

    private static async Task<IResult> CompleteAsync(
        [FromBody] AeroCmsAssistantRequest request,
        [FromServices] IAeroCmsAssistantService assistant,
        [FromServices] AeroCmsMcpInvocationContextFactory contextFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId(httpContext);
        httpContext.Response.Headers[CorrelationHeader] = correlationId;
        var contextResult = await CreateInteractiveContextAsync(contextFactory, cancellationToken);
        if (contextResult is Result<AeroCmsToolExecutionContext>.Failure contextFailure)
            return ToProblem(contextFailure.Error, correlationId);
        var executionContext =
            ((Result<AeroCmsToolExecutionContext>.Ok)contextResult).Value with
            {
                CorrelationId = correlationId
            };
        var result = await assistant.CompleteAsync(request, executionContext, cancellationToken);
        return result switch
        {
            Result<AeroCmsAssistantResponse>.Ok ok => TypedResults.Ok(ok.Value),
            Result<AeroCmsAssistantResponse>.Failure failure => ToProblem(failure.Error, correlationId),
            _ => SafeProblem(StatusCodes.Status500InternalServerError, "Assistant request failed.", correlationId)
        };
    }

    private static async Task<IResult> StreamAsync(
        [FromBody] AeroCmsAssistantRequest request,
        [FromServices] IAeroCmsAssistantService assistant,
        [FromServices] AeroCmsMcpInvocationContextFactory contextFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId(httpContext);
        httpContext.Response.Headers[CorrelationHeader] = correlationId;
        httpContext.Response.Headers.CacheControl = "no-store, no-cache";
        httpContext.Response.Headers["X-Accel-Buffering"] = "no";
        var contextResult = await CreateInteractiveContextAsync(contextFactory, cancellationToken);
        if (contextResult is Result<AeroCmsToolExecutionContext>.Failure contextFailure)
            return ToProblem(contextFailure.Error, correlationId);
        var executionContext =
            ((Result<AeroCmsToolExecutionContext>.Ok)contextResult).Value with
            {
                CorrelationId = correlationId
            };
        var result = await assistant.StreamAsync(request, executionContext, cancellationToken);
        return result switch
        {
            Result<IAsyncEnumerable<AeroCmsAssistantEvent>>.Ok ok =>
                TypedResults.ServerSentEvents(ToSseAsync(ok.Value, httpContext.RequestAborted)),
            Result<IAsyncEnumerable<AeroCmsAssistantEvent>>.Failure failure =>
                ToProblem(failure.Error, correlationId),
            _ => SafeProblem(StatusCodes.Status500InternalServerError, "Assistant stream failed.", correlationId)
        };
    }

    private static async Task<IResult> ListConversationsAsync(
        [FromServices] IAeroAiConversationStore conversationStore,
        [FromServices] AeroCmsMcpInvocationContextFactory contextFactory,
        HttpContext httpContext,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        var correlationId = GetCorrelationId(httpContext);
        SetPrivateResponseHeaders(httpContext, correlationId);
        var contextResult = await CreateInteractiveContextAsync(contextFactory, cancellationToken);
        if (contextResult is Result<AeroCmsToolExecutionContext>.Failure contextFailure)
            return ToProblem(contextFailure.Error, correlationId);
        var context = ((Result<AeroCmsToolExecutionContext>.Ok)contextResult).Value;
        var result = await conversationStore.ListAsync(
            CreateMemoryScope(context),
            take,
            cancellationToken);
        return result switch
        {
            Result<IReadOnlyList<AeroCmsAssistantConversationSummary>>.Ok ok =>
                TypedResults.Ok(ok.Value),
            Result<IReadOnlyList<AeroCmsAssistantConversationSummary>>.Failure failure =>
                ToProblem(failure.Error, correlationId),
            _ => SafeProblem(StatusCodes.Status500InternalServerError, "Conversation history failed.", correlationId)
        };
    }

    private static async Task<IResult> GetConversationAsync(
        long conversationId,
        [FromServices] IAeroAiConversationStore conversationStore,
        [FromServices] AeroCmsMcpInvocationContextFactory contextFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId(httpContext);
        SetPrivateResponseHeaders(httpContext, correlationId);
        var contextResult = await CreateInteractiveContextAsync(contextFactory, cancellationToken);
        if (contextResult is Result<AeroCmsToolExecutionContext>.Failure contextFailure)
            return ToProblem(contextFailure.Error, correlationId);
        var context = ((Result<AeroCmsToolExecutionContext>.Ok)contextResult).Value;
        var result = await conversationStore.GetAsync(
            CreateMemoryScope(context),
            conversationId,
            cancellationToken);
        return result switch
        {
            Result<AeroCmsAssistantConversation>.Ok ok => TypedResults.Ok(ok.Value),
            Result<AeroCmsAssistantConversation>.Failure failure =>
                ToProblem(failure.Error, correlationId),
            _ => SafeProblem(StatusCodes.Status500InternalServerError, "Conversation history failed.", correlationId)
        };
    }

    private static async Task<IResult> DeleteConversationAsync(
        long conversationId,
        [FromServices] IAeroAiConversationStore conversationStore,
        [FromServices] AeroCmsMcpInvocationContextFactory contextFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId(httpContext);
        SetPrivateResponseHeaders(httpContext, correlationId);
        var contextResult = await CreateInteractiveContextAsync(contextFactory, cancellationToken);
        if (contextResult is Result<AeroCmsToolExecutionContext>.Failure contextFailure)
            return ToProblem(contextFailure.Error, correlationId);
        var context = ((Result<AeroCmsToolExecutionContext>.Ok)contextResult).Value;
        var result = await conversationStore.DeleteAsync(
            CreateMemoryScope(context),
            conversationId,
            cancellationToken);
        return result switch
        {
            Result<bool>.Ok => TypedResults.NoContent(),
            Result<bool>.Failure failure => ToProblem(failure.Error, correlationId),
            _ => SafeProblem(StatusCodes.Status500InternalServerError, "Conversation deletion failed.", correlationId)
        };
    }

    private static async Task<IResult> ListMemoriesAsync(
        [FromServices] IAeroAiExplicitMemoryStore memoryStore,
        [FromServices] AeroCmsMcpInvocationContextFactory contextFactory,
        HttpContext httpContext,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        var correlationId = GetCorrelationId(httpContext);
        SetPrivateResponseHeaders(httpContext, correlationId);
        var contextResult = await CreateInteractiveContextAsync(contextFactory, cancellationToken);
        if (contextResult is Result<AeroCmsToolExecutionContext>.Failure contextFailure)
            return ToProblem(contextFailure.Error, correlationId);
        var context = ((Result<AeroCmsToolExecutionContext>.Ok)contextResult).Value;
        var result = await memoryStore.ListAsync(CreateMemoryScope(context), take, cancellationToken);
        return result switch
        {
            Result<IReadOnlyList<AeroAiExplicitMemory>>.Ok ok => TypedResults.Ok(ok.Value),
            Result<IReadOnlyList<AeroAiExplicitMemory>>.Failure failure =>
                ToProblem(failure.Error, correlationId),
            _ => SafeProblem(StatusCodes.Status500InternalServerError, "Assistant memory failed.", correlationId)
        };
    }

    private static Task<IResult> SaveMemoryAsync(
        [FromBody] AeroAiExplicitMemoryWrite memory,
        [FromServices] IAeroAiExplicitMemoryStore memoryStore,
        [FromServices] AeroCmsMcpInvocationContextFactory contextFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => SaveMemoryCoreAsync(
            memory with { MemoryId = null },
            memoryStore,
            contextFactory,
            httpContext,
            cancellationToken);

    private static Task<IResult> UpdateMemoryAsync(
        long memoryId,
        [FromBody] AeroAiExplicitMemoryWrite memory,
        [FromServices] IAeroAiExplicitMemoryStore memoryStore,
        [FromServices] AeroCmsMcpInvocationContextFactory contextFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => SaveMemoryCoreAsync(
            memory with { MemoryId = memoryId },
            memoryStore,
            contextFactory,
            httpContext,
            cancellationToken);

    private static async Task<IResult> SaveMemoryCoreAsync(
        AeroAiExplicitMemoryWrite memory,
        IAeroAiExplicitMemoryStore memoryStore,
        AeroCmsMcpInvocationContextFactory contextFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId(httpContext);
        SetPrivateResponseHeaders(httpContext, correlationId);
        var contextResult = await CreateInteractiveContextAsync(contextFactory, cancellationToken);
        if (contextResult is Result<AeroCmsToolExecutionContext>.Failure contextFailure)
            return ToProblem(contextFailure.Error, correlationId);
        var context = ((Result<AeroCmsToolExecutionContext>.Ok)contextResult).Value;
        var result = await memoryStore.SaveAsync(CreateMemoryScope(context), memory, cancellationToken);
        return result switch
        {
            Result<AeroAiExplicitMemory>.Ok ok => TypedResults.Ok(ok.Value),
            Result<AeroAiExplicitMemory>.Failure failure => ToProblem(failure.Error, correlationId),
            _ => SafeProblem(StatusCodes.Status500InternalServerError, "Assistant memory save failed.", correlationId)
        };
    }

    private static async Task<IResult> DeleteMemoryAsync(
        long memoryId,
        [FromServices] IAeroAiExplicitMemoryStore memoryStore,
        [FromServices] AeroCmsMcpInvocationContextFactory contextFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId(httpContext);
        SetPrivateResponseHeaders(httpContext, correlationId);
        var contextResult = await CreateInteractiveContextAsync(contextFactory, cancellationToken);
        if (contextResult is Result<AeroCmsToolExecutionContext>.Failure contextFailure)
            return ToProblem(contextFailure.Error, correlationId);
        var context = ((Result<AeroCmsToolExecutionContext>.Ok)contextResult).Value;
        var result = await memoryStore.DeleteAsync(
            CreateMemoryScope(context),
            memoryId,
            cancellationToken);
        return result switch
        {
            Result<bool>.Ok => TypedResults.NoContent(),
            Result<bool>.Failure failure => ToProblem(failure.Error, correlationId),
            _ => SafeProblem(StatusCodes.Status500InternalServerError, "Assistant memory deletion failed.", correlationId)
        };
    }

    private static async IAsyncEnumerable<SseItem<AeroCmsAssistantEvent>> ToSseAsync(
        IAsyncEnumerable<AeroCmsAssistantEvent> events,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in events.WithCancellation(cancellationToken))
        {
            var eventType = item.Kind.ToString().ToLowerInvariant();
            yield return new SseItem<AeroCmsAssistantEvent>(item, eventType);
        }
    }

    private static IResult ToProblem(AeroError error, string correlationId)
        => error switch
        {
            AeroError.Validation or AeroError.BadRequest or AeroError.InvalidRequest =>
                SafeProblem(StatusCodes.Status400BadRequest, "Assistant request was invalid.", correlationId),
            AeroError.Unauthorized =>
                SafeProblem(StatusCodes.Status401Unauthorized, "Authentication is required.", correlationId),
            AeroError.Forbidden =>
                SafeProblem(StatusCodes.Status403Forbidden, "The request is not authorized for this site.", correlationId),
            AeroError.Timeout =>
                SafeProblem(StatusCodes.Status504GatewayTimeout, "Assistant request timed out.", correlationId),
            AeroError.Cancelled =>
                SafeProblem(499, "Assistant request was cancelled.", correlationId),
            AeroError.Configuration =>
                SafeProblem(StatusCodes.Status503ServiceUnavailable, "Assistant is not configured.", correlationId),
            AeroError.NotAllowed =>
                SafeProblem(StatusCodes.Status429TooManyRequests, "The AI token budget is exhausted.", correlationId),
            _ => SafeProblem(StatusCodes.Status502BadGateway, "Assistant provider invocation failed.", correlationId)
        };

    private static IResult SafeProblem(int statusCode, string detail, string correlationId)
        => Results.Problem(
            statusCode: statusCode,
            title: "AeroCMS assistant request failed",
            detail: detail,
            extensions: new Dictionary<string, object?> { ["correlationId"] = correlationId });

    private static async Task<Result<AeroCmsToolExecutionContext>> CreateInteractiveContextAsync(
        AeroCmsMcpInvocationContextFactory contextFactory,
        CancellationToken cancellationToken)
    {
        var result = await contextFactory.CreateAsync(cancellationToken);
        if (result is not Result<AeroCmsToolExecutionContext>.Ok ok)
            return ((Result<AeroCmsToolExecutionContext>.Failure)result).Error;
        if (ok.Value.Principal.HasClaim(
                claim => string.Equals(
                    claim.Type,
                    AeroApiKeyClaimTypes.KeyId,
                    StringComparison.Ordinal)))
        {
            return AeroError.ForbiddenError(
                "API keys cannot access a user's assistant conversations.");
        }
        return ok.Value;
    }

    private static AeroAiMemoryScope CreateMemoryScope(
        AeroCmsToolExecutionContext context)
        => new(
            context.TenantId,
            context.SiteId,
            AeroAiAudience.Manager,
            AeroAiPrincipalKind.ManagerUser,
            context.UserId,
            CultureInfo.CurrentUICulture.Name);

    private static void SetPrivateResponseHeaders(
        HttpContext httpContext,
        string correlationId)
    {
        httpContext.Response.Headers[CorrelationHeader] = correlationId;
        httpContext.Response.Headers.CacheControl = "no-store, no-cache";
    }

    private static string GetCorrelationId(HttpContext httpContext)
    {
        var value = httpContext.TraceIdentifier;
        if (string.IsNullOrWhiteSpace(value))
            value = "assistant";
        return value.Length <= 128 ? value : value[..128];
    }
}
