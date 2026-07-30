using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Modules.Commerce.Catalog.Services;
using Aero.Core.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Commerce.Subscriptions.Api;

/// <summary>Maps redacted subscription read models. These endpoints never expose provider transport details.</summary>
public static class SubscriptionVisibilityEndpoints
{
    public static IEndpointRouteBuilder MapSubscriptionVisibilityApi(this IEndpointRouteBuilder builder)
    {
        var member = builder.MapGroup("/api/commerce/subscriptions")
            .RequireAuthorization(ExternalMemberAuthenticationDefaults.Policy, ExternalMemberAuthenticationDefaults.SitePolicy);
        member.MapGet("/", ListForMember);
        member.MapGet("/orders/{orderId:long}", GetForMemberOrder);

        var manager = builder.MapGroup("/api/v1/admin/commerce/subscriptions");
        manager.MapGet("/", ListForManager).RequireAuthorization("site:read");
        manager.MapGet("/{subscriptionId:long}", GetForManager).RequireAuthorization("site:read");
        return builder;
    }

    private static async Task<IResult> ListForMember(HttpContext context, ISubscriptionVisibilityService visibility, ICurrentPrincipal principal, ISiteContext site, CancellationToken ct)
    {
        SetNoStore(context);
        var memberId = principal.PrincipalId;
        if (memberId is not > 0) return Results.NotFound();
        var result = await visibility.ListForMemberAsync(site.TenantId, site.SiteId, memberId.Value, ct);
        return result is Result<IReadOnlyList<MemberSubscriptionSummary>, AeroError>.Ok ok ? Results.Ok(ok.Value) : Results.Problem("Subscription history could not be loaded.", statusCode: StatusCodes.Status500InternalServerError);
    }

    private static async Task<IResult> GetForMemberOrder(HttpContext context, long orderId, ISubscriptionVisibilityService visibility, ICurrentPrincipal principal, ISiteContext site, CancellationToken ct)
    {
        SetNoStore(context);
        var memberId = principal.PrincipalId;
        if (memberId is not > 0) return Results.NotFound();
        var result = await visibility.GetForMemberOrderAsync(site.TenantId, site.SiteId, memberId.Value, orderId, ct);
        return result switch
        {
            Result<MemberSubscriptionReceipt?, AeroError>.Ok { Value: { } receipt } => Results.Ok(receipt),
            Result<MemberSubscriptionReceipt?, AeroError>.Ok => Results.NotFound(),
            _ => Results.Problem("Subscription receipt could not be loaded.", statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    private static async Task<IResult> ListForManager(HttpContext context, int? skip, int? take, ISubscriptionVisibilityService visibility, ICommerceManagerScopeResolver scopeResolver, CancellationToken ct)
    {
        SetNoStore(context);
        var scope = await scopeResolver.ResolveAsync(ct);
        if (scope is not Result<CommerceManagerScope, AeroError>.Ok scopeOk) return Results.NotFound();
        var result = await visibility.ListForManagerAsync(scopeOk.Value.TenantId, scopeOk.Value.SiteId, skip ?? 0, take ?? 20, ct);
        return result is Result<ManagerSubscriptionPage, AeroError>.Ok ok ? Results.Ok(ok.Value) : Results.Problem("Subscription status could not be loaded.", statusCode: StatusCodes.Status500InternalServerError);
    }

    private static async Task<IResult> GetForManager(HttpContext context, long subscriptionId, ISubscriptionVisibilityService visibility, ICommerceManagerScopeResolver scopeResolver, CancellationToken ct)
    {
        SetNoStore(context);
        var scope = await scopeResolver.ResolveAsync(ct);
        if (scope is not Result<CommerceManagerScope, AeroError>.Ok scopeOk) return Results.NotFound();
        var result = await visibility.GetForManagerAsync(scopeOk.Value.TenantId, scopeOk.Value.SiteId, subscriptionId, ct);
        return result switch
        {
            Result<ManagerSubscriptionReceipt?, AeroError>.Ok { Value: { } receipt } => Results.Ok(receipt),
            Result<ManagerSubscriptionReceipt?, AeroError>.Ok => Results.NotFound(),
            _ => Results.Problem("Subscription receipt could not be loaded.", statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    private static void SetNoStore(HttpContext context) => context.Response.Headers.CacheControl = "no-store";
}
