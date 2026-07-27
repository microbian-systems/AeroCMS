using System.Globalization;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using Aero.Cms.Abstractions.Ai.Assistant;
using Aero.Cms.Abstractions.Ai.Knowledge;
using Aero.Cms.Abstractions.Ai.Memory;
using Aero.Cms.Abstractions.Ai.Pipeline;
using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Modules.RateLimiting;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.AiAssistant;

/// <summary>
/// Maps public-corpus-only assistant endpoints for anonymous visitors and authenticated members.
/// </summary>
public static class AeroSiteAssistantEndpoints
{
    private const string CorrelationHeader = "X-Correlation-Id";

    public static IEndpointRouteBuilder MapAeroSiteAssistantEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var publicGroup = endpoints.MapGroup("/api/v1/ai/assistant")
            .AllowAnonymous()
            .RequireRateLimiting(AeroRateLimitPolicyNames.AiPublic);
        publicGroup.MapPost("/complete", CompletePublicAsync);
        publicGroup.MapPost("/stream", StreamPublicAsync)
            .RequireRateLimiting(AeroRateLimitPolicyNames.AiStream);
        endpoints.MapGet("/api/v1/ai/search", SearchPublicAsync)
            .AllowAnonymous()
            .RequireRateLimiting(AeroRateLimitPolicyNames.AiPublic);

        var memberGroup = endpoints.MapGroup("/api/v1/member/assistant")
            .RequireAuthorization(
                ExternalMemberAuthenticationDefaults.Policy,
                ExternalMemberAuthenticationDefaults.SitePolicy)
            .RequireRateLimiting(AeroRateLimitPolicyNames.AiMember);
        memberGroup.MapPost("/complete", CompleteMemberAsync)
            .WithMetadata(new RequireAntiforgeryTokenAttribute());
        memberGroup.MapPost("/stream", StreamMemberAsync)
            .RequireRateLimiting(AeroRateLimitPolicyNames.AiStream)
            .WithMetadata(new RequireAntiforgeryTokenAttribute());
        memberGroup.MapGet("/conversations", ListMemberConversationsAsync);
        memberGroup.MapGet("/conversations/{conversationId:long}", GetMemberConversationAsync);
        memberGroup.MapDelete("/conversations/{conversationId:long}", DeleteMemberConversationAsync)
            .WithMetadata(new RequireAntiforgeryTokenAttribute());
        return endpoints;
    }

    private static async Task<IResult> SearchPublicAsync(
        [FromServices] IAeroAiKnowledgeRetriever retriever,
        [FromServices] ISiteContext siteContext,
        HttpContext httpContext,
        string q,
        int take = 10,
        CancellationToken cancellationToken = default)
    {
        var correlationId = SetPrivateHeaders(httpContext);
        if (siteContext.TenantId <= 0 || siteContext.SiteId <= 0)
            return SiteUnavailable(correlationId);
        if (string.IsNullOrWhiteSpace(q) || q.Length > 512 || take is < 1 or > 20)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                "Search requires a bounded query and take between 1 and 20.",
                correlationId);
        }

        var culture = CultureInfo.CurrentUICulture.Name;
        var result = await retriever.SearchAsync(
            new AeroAiKnowledgeQuery(
                siteContext.TenantId,
                siteContext.SiteId,
                AeroAiAudience.Public,
                culture,
                q.Trim(),
                take),
            cancellationToken);
        return result switch
        {
            Result<IReadOnlyList<AeroAiKnowledgeMatch>>.Ok ok =>
                TypedResults.Ok(new AeroAiPublicSearchResult(
                    ok.Value.Select(match => new AeroAiPublicSearchItem(
                        match.SourceKind,
                        match.SourceId.ToString(CultureInfo.InvariantCulture),
                        match.SourceUri,
                        match.Title,
                        match.Section,
                        CreateExcerpt(match.Content))).ToArray(),
                    culture)),
            Result<IReadOnlyList<AeroAiKnowledgeMatch>>.Failure failure =>
                ToProblem(failure.Error, correlationId),
            _ => Problem(StatusCodes.Status500InternalServerError, "Public search failed.", correlationId)
        };
    }

    private static Task<IResult> CompletePublicAsync(
        [FromBody] AeroCmsAssistantRequest request,
        [FromServices] IAeroCmsSiteAssistantService assistant,
        [FromServices] ISiteContext siteContext,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => CompleteAsync(
            request,
            assistant,
            CreatePublicContext(siteContext, httpContext),
            httpContext,
            cancellationToken);

    private static Task<IResult> CompleteMemberAsync(
        [FromBody] AeroCmsAssistantRequest request,
        [FromServices] IAeroCmsSiteAssistantService assistant,
        [FromServices] ISiteContext siteContext,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var context = CreateMemberContext(siteContext, httpContext);
        return context is null
            ? Task.FromResult(Forbidden(httpContext))
            : CompleteAsync(request, assistant, context, httpContext, cancellationToken);
    }

    private static async Task<IResult> CompleteAsync(
        AeroCmsAssistantRequest request,
        IAeroCmsSiteAssistantService assistant,
        AeroCmsSiteAssistantContext? context,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var correlationId = SetPrivateHeaders(httpContext);
        if (context is null)
            return SiteUnavailable(correlationId);
        context = context with { CorrelationId = correlationId };
        var result = await assistant.CompleteAsync(request, context, cancellationToken);
        return result switch
        {
            Result<AeroCmsAssistantResponse>.Ok ok => TypedResults.Ok(ok.Value),
            Result<AeroCmsAssistantResponse>.Failure failure => ToProblem(failure.Error, correlationId),
            _ => Problem(StatusCodes.Status500InternalServerError, "Assistant request failed.", correlationId)
        };
    }

    private static Task<IResult> StreamPublicAsync(
        [FromBody] AeroCmsAssistantRequest request,
        [FromServices] IAeroCmsSiteAssistantService assistant,
        [FromServices] ISiteContext siteContext,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => StreamAsync(
            request,
            assistant,
            CreatePublicContext(siteContext, httpContext),
            httpContext,
            cancellationToken);

    private static Task<IResult> StreamMemberAsync(
        [FromBody] AeroCmsAssistantRequest request,
        [FromServices] IAeroCmsSiteAssistantService assistant,
        [FromServices] ISiteContext siteContext,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var context = CreateMemberContext(siteContext, httpContext);
        return context is null
            ? Task.FromResult(Forbidden(httpContext))
            : StreamAsync(request, assistant, context, httpContext, cancellationToken);
    }

    private static async Task<IResult> StreamAsync(
        AeroCmsAssistantRequest request,
        IAeroCmsSiteAssistantService assistant,
        AeroCmsSiteAssistantContext? context,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var correlationId = SetPrivateHeaders(httpContext);
        httpContext.Response.Headers["X-Accel-Buffering"] = "no";
        if (context is null)
            return SiteUnavailable(correlationId);
        context = context with { CorrelationId = correlationId };
        var result = await assistant.StreamAsync(request, context, cancellationToken);
        return result switch
        {
            Result<IAsyncEnumerable<AeroCmsAssistantEvent>>.Ok ok =>
                TypedResults.ServerSentEvents(ToSseAsync(ok.Value, httpContext.RequestAborted)),
            Result<IAsyncEnumerable<AeroCmsAssistantEvent>>.Failure failure =>
                ToProblem(failure.Error, correlationId),
            _ => Problem(StatusCodes.Status500InternalServerError, "Assistant stream failed.", correlationId)
        };
    }

    private static async Task<IResult> ListMemberConversationsAsync(
        [FromServices] IAeroAiConversationStore conversationStore,
        [FromServices] ISiteContext siteContext,
        HttpContext httpContext,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        var correlationId = SetPrivateHeaders(httpContext);
        var context = CreateMemberContext(siteContext, httpContext);
        if (context is null)
            return Problem(StatusCodes.Status403Forbidden, "Member scope is unavailable.", correlationId);
        var result = await conversationStore.ListAsync(
            CreateMemberMemoryScope(context),
            take,
            cancellationToken);
        return result switch
        {
            Result<IReadOnlyList<AeroCmsAssistantConversationSummary>>.Ok ok =>
                TypedResults.Ok(ok.Value),
            Result<IReadOnlyList<AeroCmsAssistantConversationSummary>>.Failure failure =>
                ToProblem(failure.Error, correlationId),
            _ => Problem(StatusCodes.Status500InternalServerError, "Conversation history failed.", correlationId)
        };
    }

    private static async Task<IResult> GetMemberConversationAsync(
        long conversationId,
        [FromServices] IAeroAiConversationStore conversationStore,
        [FromServices] ISiteContext siteContext,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var correlationId = SetPrivateHeaders(httpContext);
        var context = CreateMemberContext(siteContext, httpContext);
        if (context is null)
            return Problem(StatusCodes.Status403Forbidden, "Member scope is unavailable.", correlationId);
        var result = await conversationStore.GetAsync(
            CreateMemberMemoryScope(context),
            conversationId,
            cancellationToken);
        return result switch
        {
            Result<AeroCmsAssistantConversation>.Ok ok => TypedResults.Ok(ok.Value),
            Result<AeroCmsAssistantConversation>.Failure failure => ToProblem(failure.Error, correlationId),
            _ => Problem(StatusCodes.Status500InternalServerError, "Conversation history failed.", correlationId)
        };
    }

    private static async Task<IResult> DeleteMemberConversationAsync(
        long conversationId,
        [FromServices] IAeroAiConversationStore conversationStore,
        [FromServices] ISiteContext siteContext,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var correlationId = SetPrivateHeaders(httpContext);
        var context = CreateMemberContext(siteContext, httpContext);
        if (context is null)
            return Problem(StatusCodes.Status403Forbidden, "Member scope is unavailable.", correlationId);
        var result = await conversationStore.DeleteAsync(
            CreateMemberMemoryScope(context),
            conversationId,
            cancellationToken);
        return result switch
        {
            Result<bool>.Ok => TypedResults.NoContent(),
            Result<bool>.Failure failure => ToProblem(failure.Error, correlationId),
            _ => Problem(StatusCodes.Status500InternalServerError, "Conversation deletion failed.", correlationId)
        };
    }

    private static AeroCmsSiteAssistantContext? CreatePublicContext(
        ISiteContext siteContext,
        HttpContext httpContext)
    {
        if (siteContext.TenantId <= 0 || siteContext.SiteId <= 0)
            return null;
        return new(
            AeroAiAudience.Public,
            new ClaimsPrincipal(new ClaimsIdentity()),
            PrincipalId: 0,
            siteContext.TenantId,
            siteContext.SiteId,
            CultureInfo.CurrentUICulture.Name,
            httpContext.TraceIdentifier);
    }

    private static AeroCmsSiteAssistantContext? CreateMemberContext(
        ISiteContext siteContext,
        HttpContext httpContext)
    {
        var principal = httpContext.User;
        var identities = principal.Identities.ToArray();
        if (siteContext.TenantId <= 0 ||
            siteContext.SiteId <= 0 ||
            identities.Length != 1 ||
            !identities[0].IsAuthenticated ||
            !string.Equals(
                identities[0].AuthenticationType,
                ExternalMemberAuthenticationDefaults.Scheme,
                StringComparison.Ordinal) ||
            HasForbiddenClaims(principal) ||
            !TryReadExactlyOne(principal, ClaimTypes.NameIdentifier, out var memberIdText) ||
            !TryReadExactlyOne(
                principal,
                ExternalMemberClaimTypes.PrincipalKind,
                out var principalKind) ||
            !string.Equals(
                principalKind,
                ExternalMemberClaimTypes.ExternalMember,
                StringComparison.Ordinal) ||
            !long.TryParse(
                memberIdText,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var memberId) ||
            memberId <= 0)
        {
            return null;
        }

        return new(
            AeroAiAudience.Member,
            principal,
            memberId,
            siteContext.TenantId,
            siteContext.SiteId,
            CultureInfo.CurrentUICulture.Name,
            httpContext.TraceIdentifier);
    }

    private static AeroAiMemoryScope CreateMemberMemoryScope(AeroCmsSiteAssistantContext context)
        => new(
            context.TenantId,
            context.SiteId,
            AeroAiAudience.Member,
            AeroAiPrincipalKind.Member,
            context.PrincipalId,
            context.Culture);

    private static bool TryReadExactlyOne(
        ClaimsPrincipal principal,
        string claimType,
        out string value)
    {
        var values = principal.FindAll(claimType).Select(claim => claim.Value).ToArray();
        value = values.Length == 1 ? values[0] : string.Empty;
        return values.Length == 1;
    }

    private static bool HasForbiddenClaims(ClaimsPrincipal principal)
        => principal.Claims.Any(claim =>
            claim.Type == ClaimTypes.Role ||
            string.Equals(claim.Type, "role", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(claim.Type, "roles", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(claim.Type, "is_admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(claim.Type, "permission", StringComparison.OrdinalIgnoreCase));

    private static string CreateExcerpt(string content)
    {
        var normalized = string.Join(
            ' ',
            content.Split(
                ['\r', '\n', '\t'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return normalized.Length <= 320 ? normalized : normalized[..320];
    }

    private static async IAsyncEnumerable<SseItem<AeroCmsAssistantEvent>> ToSseAsync(
        IAsyncEnumerable<AeroCmsAssistantEvent> events,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in events.WithCancellation(cancellationToken))
            yield return new SseItem<AeroCmsAssistantEvent>(
                item,
                item.Kind.ToString().ToLowerInvariant());
    }

    private static IResult ToProblem(AeroError error, string correlationId)
        => error switch
        {
            AeroError.Validation or AeroError.BadRequest or AeroError.InvalidRequest =>
                Problem(StatusCodes.Status400BadRequest, "Assistant request was invalid.", correlationId),
            AeroError.Unauthorized =>
                Problem(StatusCodes.Status401Unauthorized, "Authentication is required.", correlationId),
            AeroError.Forbidden =>
                Problem(StatusCodes.Status403Forbidden, "The request is not authorized for this site.", correlationId),
            AeroError.Timeout =>
                Problem(StatusCodes.Status504GatewayTimeout, "Assistant request timed out.", correlationId),
            AeroError.Cancelled =>
                Problem(499, "Assistant request was cancelled.", correlationId),
            AeroError.Configuration =>
                Problem(StatusCodes.Status503ServiceUnavailable, "Assistant is unavailable.", correlationId),
            AeroError.NotAllowed =>
                Problem(StatusCodes.Status429TooManyRequests, "The AI token budget is exhausted.", correlationId),
            _ => Problem(StatusCodes.Status502BadGateway, "Assistant provider invocation failed.", correlationId)
        };

    private static IResult SiteUnavailable(string correlationId)
        => Problem(StatusCodes.Status404NotFound, "Site assistant is unavailable.", correlationId);

    private static IResult Forbidden(HttpContext httpContext)
        => Problem(
            StatusCodes.Status403Forbidden,
            "Member scope is unavailable.",
            SetPrivateHeaders(httpContext));

    private static IResult Problem(int statusCode, string detail, string correlationId)
        => Results.Problem(
            statusCode: statusCode,
            title: "AeroCMS site assistant request failed",
            detail: detail,
            extensions: new Dictionary<string, object?> { ["correlationId"] = correlationId });

    private static string SetPrivateHeaders(HttpContext httpContext)
    {
        var correlationId = string.IsNullOrWhiteSpace(httpContext.TraceIdentifier)
            ? "assistant"
            : httpContext.TraceIdentifier;
        if (correlationId.Length > 128)
            correlationId = correlationId[..128];
        httpContext.Response.Headers[CorrelationHeader] = correlationId;
        httpContext.Response.Headers.CacheControl = "no-store, no-cache";
        return correlationId;
    }
}
