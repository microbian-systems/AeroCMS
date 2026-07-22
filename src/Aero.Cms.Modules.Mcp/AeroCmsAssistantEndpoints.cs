using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using Aero.Cms.Abstractions.Ai.Assistant;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Mcp;

/// <summary>Maps the authenticated REST and SSE manager assistant boundary.</summary>
public static class AeroCmsAssistantEndpoints
{
    private const string CorrelationHeader = "X-Correlation-Id";

    public static IEndpointRouteBuilder MapAeroCmsAssistantEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/admin/mcp/assistant")
            .RequireAuthorization()
            .RequireAuthorization("site:read");

        group.MapPost("/complete", CompleteAsync);
        group.MapPost("/stream", StreamAsync);
        return endpoints;
    }

    private static async Task<IResult> CompleteAsync(
        AeroCmsAssistantRequest request,
        IAeroCmsAssistantService assistant,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId(httpContext);
        httpContext.Response.Headers[CorrelationHeader] = correlationId;
        var result = await assistant.CompleteAsync(request, correlationId, cancellationToken);
        return result switch
        {
            Result<AeroCmsAssistantResponse>.Ok ok => TypedResults.Ok(ok.Value),
            Result<AeroCmsAssistantResponse>.Failure failure => ToProblem(failure.Error, correlationId),
            _ => SafeProblem(StatusCodes.Status500InternalServerError, "Assistant request failed.", correlationId)
        };
    }

    private static async Task<IResult> StreamAsync(
        AeroCmsAssistantRequest request,
        IAeroCmsAssistantService assistant,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId(httpContext);
        httpContext.Response.Headers[CorrelationHeader] = correlationId;
        var result = await assistant.StreamAsync(request, correlationId, cancellationToken);
        return result switch
        {
            Result<IAsyncEnumerable<AeroCmsAssistantEvent>>.Ok ok =>
                TypedResults.ServerSentEvents(ToSseAsync(ok.Value, httpContext.RequestAborted)),
            Result<IAsyncEnumerable<AeroCmsAssistantEvent>>.Failure failure =>
                ToProblem(failure.Error, correlationId),
            _ => SafeProblem(StatusCodes.Status500InternalServerError, "Assistant stream failed.", correlationId)
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
            _ => SafeProblem(StatusCodes.Status502BadGateway, "Assistant provider invocation failed.", correlationId)
        };

    private static IResult SafeProblem(int statusCode, string detail, string correlationId)
        => Results.Problem(
            statusCode: statusCode,
            title: "AeroCMS assistant request failed",
            detail: detail,
            extensions: new Dictionary<string, object?> { ["correlationId"] = correlationId });

    private static string GetCorrelationId(HttpContext httpContext)
    {
        var value = httpContext.TraceIdentifier;
        if (string.IsNullOrWhiteSpace(value))
            value = "assistant";
        return value.Length <= 128 ? value : value[..128];
    }
}
