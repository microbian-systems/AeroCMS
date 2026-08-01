using System.Globalization;
using System.Security.Claims;
using Aero.Cms.Abstractions.Security;
using Aero.Cms.Abstractions.Services;
using Aero.Cms.Modules.RateLimiting;
using Aero.Cms.Modules.Sites;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Mcp;

/// <summary>
/// Admin-only management boundary for tenant- and site-scoped MCP service keys.
/// </summary>
public static class AeroMcpApiKeyEndpoints
{
    private const string SiteCookieName = "AeroCms.SiteId";
    private const int MaximumSitesPerKey = 100;

    public static IEndpointRouteBuilder MapAeroMcpApiKeyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/admin/mcp/api-keys")
            .RequireAuthorization("AeroAdmin")
            .RequireAuthorization("site:read")
            .RequireRateLimiting(AeroRateLimitPolicyNames.McpManagement);

        group.MapGet("/", ListAsync);
        group.MapPost("/", CreateAsync);
        group.MapDelete("/{keyId:long}", RevokeAsync);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        HttpContext httpContext,
        [FromServices] ISiteContext siteContext,
        [FromServices] ISiteService siteService,
        [FromServices] IApiKeyService apiKeyService,
        CancellationToken cancellationToken)
    {
        var scope = await ResolveManagerScopeAsync(
            httpContext,
            siteContext,
            siteService,
            cancellationToken);
        if (scope is null)
            return ScopeNotFound();

        var keys = await apiKeyService.ListAsync(
            scope.UserId,
            scope.TenantId,
            cancellationToken);
        return TypedResults.Ok(keys);
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] CreateMcpApiKeyHttpRequest request,
        HttpContext httpContext,
        [FromServices] ISiteContext siteContext,
        [FromServices] ISiteService siteService,
        [FromServices] IApiKeyService apiKeyService,
        CancellationToken cancellationToken)
    {
        var scope = await ResolveManagerScopeAsync(
            httpContext,
            siteContext,
            siteService,
            cancellationToken);
        if (scope is null)
            return ScopeNotFound();

        var requestedSiteIds = (request.AllowedSiteIds is { Count: > 0 }
                ? request.AllowedSiteIds
                : [scope.SiteId])
            .Distinct()
            .ToArray();
        if (requestedSiteIds.Length == 0 ||
            requestedSiteIds.Length > MaximumSitesPerKey ||
            requestedSiteIds.Any(siteId => siteId <= 0))
        {
            return ValidationProblem(
                $"A key must include between 1 and {MaximumSitesPerKey} valid site identifiers.");
        }

        foreach (var siteId in requestedSiteIds)
        {
            var siteResult = await siteService.GetSiteByIdAsync(siteId, cancellationToken);
            if (siteResult is not Option<Aero.Cms.Core.Entities.SitesModel>.Some site ||
                !site.Value.IsEnabled ||
                site.Value.TenantId != scope.TenantId)
            {
                return ValidationProblem(
                    "Every allowed site must be enabled and belong to the selected tenant.");
            }
        }

        try
        {
            var issued = await apiKeyService.CreateScopedKeyAsync(
                new CreateScopedApiKeyRequest(
                    scope.UserId,
                    scope.TenantId,
                    requestedSiteIds,
                    request.Name,
                    request.IsTest,
                    McpServer: true,
                    request.IsAdministrator,
                    request.Permissions ?? [],
                    request.ExpiresAt,
                    scope.UserId.ToString(CultureInfo.InvariantCulture)),
                cancellationToken);
            return TypedResults.Created(
                $"/api/v1/admin/mcp/api-keys/{issued.KeyId}",
                new IssuedMcpApiKeyHttpResponse(
                    issued.KeyId,
                    issued.RawApiKey,
                    scope.TenantId,
                    requestedSiteIds,
                    request.IsAdministrator,
                    request.Permissions ?? [],
                    request.ExpiresAt));
        }
        catch (ArgumentException exception)
        {
            return ValidationProblem(exception.Message);
        }
    }

    private static async Task<IResult> RevokeAsync(
        long keyId,
        HttpContext httpContext,
        [FromServices] ISiteContext siteContext,
        [FromServices] ISiteService siteService,
        [FromServices] IApiKeyService apiKeyService,
        CancellationToken cancellationToken)
    {
        var scope = await ResolveManagerScopeAsync(
            httpContext,
            siteContext,
            siteService,
            cancellationToken);
        if (scope is null)
            return ScopeNotFound();

        var revoked = await apiKeyService.RevokeAsync(
            keyId,
            scope.UserId,
            scope.TenantId,
            scope.UserId.ToString(CultureInfo.InvariantCulture),
            cancellationToken);
        return revoked ? TypedResults.NoContent() : TypedResults.NotFound();
    }

    private static async Task<ManagerMcpKeyScope?> ResolveManagerScopeAsync(
        HttpContext httpContext,
        ISiteContext siteContext,
        ISiteService siteService,
        CancellationToken cancellationToken)
    {
        var userIdText = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var siteIdText = httpContext.Request.Cookies[SiteCookieName];
        if (!long.TryParse(userIdText, NumberStyles.None, CultureInfo.InvariantCulture, out var userId) ||
            userId <= 0 ||
            !long.TryParse(siteIdText, NumberStyles.None, CultureInfo.InvariantCulture, out var siteId) ||
            siteId <= 0 ||
            siteContext.SiteId != siteId)
        {
            return null;
        }

        var siteResult = await siteService.GetSiteByIdAsync(siteId, cancellationToken);
        return siteResult is Option<Aero.Cms.Core.Entities.SitesModel>.Some site &&
               site.Value is { TenantId: > 0, IsEnabled: true }
            ? new ManagerMcpKeyScope(userId, site.Value.TenantId, site.Value.Id)
            : null;
    }

    private static IResult ScopeNotFound() =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "MCP key scope was not found.",
            detail: "Select an enabled site before managing MCP keys.");

    private static IResult ValidationProblem(string detail) =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "MCP API-key request was invalid.",
            detail: detail);

    private sealed record ManagerMcpKeyScope(long UserId, long TenantId, long SiteId);
}

public sealed record CreateMcpApiKeyHttpRequest(
    string Name,
    IReadOnlyList<long>? AllowedSiteIds,
    IReadOnlyList<string>? Permissions,
    bool IsAdministrator = false,
    bool IsTest = false,
    DateTimeOffset? ExpiresAt = null);

public sealed record IssuedMcpApiKeyHttpResponse(
    long KeyId,
    string RawApiKey,
    long TenantId,
    IReadOnlyList<long> AllowedSiteIds,
    bool IsAdministrator,
    IReadOnlyList<string> Permissions,
    DateTimeOffset? ExpiresAt);
