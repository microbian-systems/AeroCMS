using System.Globalization;
using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Modules.Commerce.Orders.Domain;
using Aero.Cms.Modules.Commerce.Orders.Events;
using Aero.Cms.Modules.Commerce.Orders.Services;
using Aero.Core.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Aero.Cms.Modules.Commerce.Orders.Api;

/// <summary>Maps external-member order operations constrained to the member's host site.</summary>
public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderApi(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("/api/commerce/orders").RequireAuthorization(ExternalMemberAuthenticationDefaults.Policy, ExternalMemberAuthenticationDefaults.SitePolicy);
        group.MapGet("/", async (int skip, int take, IOrderService service, ICurrentPrincipal principal, ISiteContext site, CancellationToken ct) =>
        { var result = await service.GetForMemberAsync(site.TenantId, site.SiteId, Member(principal), skip, take == 0 ? 20 : take, ct); return result is Result<(IReadOnlyList<OrderEntity> Items, long TotalCount), AeroError>.Ok(var page) ? Results.Ok(page) : Results.BadRequest(); });
        group.MapGet("/{id:long}", async (long id, IOrderService service, ICurrentPrincipal principal, ISiteContext site, CancellationToken ct) =>
        { var result = await service.GetForMemberAsync(site.TenantId, site.SiteId, Member(principal), id, ct); return result is Result<OrderEntity?, AeroError>.Ok(var order) && order is not null ? Results.Ok(order) : Results.NotFound(); });
        group.MapPost("/", async (CheckoutRequest request, IOrderService service, ICurrentPrincipal principal, ISiteContext site, IMessageBus bus, ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            var result = await service.CheckoutAsync(site.TenantId, site.SiteId, Member(principal), request.ShippingAddress, request.BillingAddress, CultureInfo.CurrentUICulture.Name, ct);
            if (result is not Result<OrderEntity, AeroError>.Ok(var order)) return Results.BadRequest();
            try { await bus.PublishAsync(new OrderStarted(order.Id, order.TenantId, order.SiteId, order.ExternalMemberId)); await bus.PublishAsync(new OrderStatusChangedToSubmitted(order.Id, order.TenantId, order.SiteId, order.ExternalMemberId, order.TotalAmount)); }
            catch (Exception ex) { loggerFactory.CreateLogger("Commerce.OrderEvents").LogError(ex, "Order {OrderId} committed but follow-up publication failed", order.Id); }
            return Results.Created($"/api/commerce/orders/{order.Id}", order);
        });
        group.MapPost("/{id:long}/cancel", async (long id, IOrderService service, ICurrentPrincipal principal, ISiteContext site, IMessageBus bus, ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            var result = await service.CancelAsync(site.TenantId, site.SiteId, Member(principal), id, ct);
            if (result is not Result<OrderEntity, AeroError>.Ok(var order)) return Results.NotFound();
            try { await bus.PublishAsync(new OrderStatusChangedToCancelled(order.Id, order.TenantId, order.SiteId, order.ExternalMemberId, "Customer requested cancellation")); }
            catch (Exception ex) { loggerFactory.CreateLogger("Commerce.OrderEvents").LogError(ex, "Order {OrderId} cancellation committed but follow-up publication failed", order.Id); }
            return Results.NoContent();
        });
        return builder;
    }
    private static long Member(ICurrentPrincipal principal) => principal.PrincipalId ?? throw new BadHttpRequestException("External member is required.");
}

/// <summary>Checkout input contains addresses only; identity, ownership, basket, and price are server-derived.</summary>
public sealed record CheckoutRequest(Address ShippingAddress, Address? BillingAddress = null);
