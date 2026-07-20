using Aero.Cms.Modules.Commerce.Orders.Domain;
using Aero.Cms.Modules.Commerce.Orders.Events;
using Aero.Cms.Modules.Commerce.Orders.Handlers;
using Aero.Cms.Modules.Commerce.Orders.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wolverine;

namespace Aero.Cms.Modules.Commerce.Orders.Api;

/// <summary>
/// Registers authenticated HTTP endpoints for querying, creating, and cancelling orders.
/// </summary>
/// <remarks>
/// The group requires authorization, but the create request supplies <c>CustomerId</c> and the query routes do not
/// filter by the authenticated principal, customer, tenant, or site. Authorization and ownership enforcement must
/// therefore be supplied elsewhere.
/// </remarks>
public static class OrderEndpoints
{
    /// <summary>
    /// Maps the <c>/api/commerce/orders</c> route group.
    /// </summary>
    /// <param name="builder">The route builder to which the authenticated routes are added.</param>
    /// <returns>The supplied <paramref name="builder"/>.</returns>
    /// <remarks>
    /// POST <c>/</c> invokes a Wolverine order command and returns 202 only after that invocation completes. The
    /// cancellation route publishes an event and immediately returns 204; it does not verify that a transition is
    /// valid or that the order exists. No route accepts an idempotency key or applies endpoint-level validation to
    /// <see cref="CreateOrderRequest"/>.
    /// </remarks>
    public static IEndpointRouteBuilder MapOrderApi(this IEndpointRouteBuilder builder)
    {
        var group = builder
            .MapGroup("/api/commerce/orders")
            .RequireAuthorization();

        // List orders
        group.MapGet("/", async (
            int skip = 0,
            int take = 20,
            IOrderService? service = null) =>
        {
            var orders = await service!.GetAllAsync();
            var items = orders.Skip(skip).Take(take).ToList();
            return Results.Ok(new { Items = items, TotalCount = items.Count });
        });

        // Get by ID
        group.MapGet("/{id:long}", async (
            long id,
            IOrderService? service = null) =>
        {
            try
            {
                var order = await service!.FindByIdAsync(id);
                return Results.Ok(order);
            }
            catch
            {
                return Results.NotFound();
            }
        });

        // Create order from basket
        group.MapPost("/", async (
            CreateOrderRequest request,
            IMessageBus bus) =>
        {
            await bus.InvokeAsync(new CreateOrder(
                request.CustomerId,
                request.ShippingAddress,
                request.BillingAddress
            ));

            return Results.Accepted();
        });

        // Cancel order
        group.MapPost("/{id:long}/cancel", async (
            long id,
            IMessageBus bus) =>
        {
            await bus.PublishAsync(new OrderStatusChangedToCancelled(id, "User requested cancellation"));
            return Results.NoContent();
        });

        return builder;
    }
}

/// <summary>
/// Request payload used to invoke commerce order creation from a basket.
/// </summary>
/// <param name="CustomerId">The caller-supplied customer identifier passed to the command; it is not derived from claims here.</param>
/// <param name="ShippingAddress">The requested shipping address passed to the command.</param>
/// <param name="BillingAddress">The optional billing address; the handler uses the shipping address when it is absent.</param>
public sealed record CreateOrderRequest(
    string CustomerId,
    Address ShippingAddress,
    Address? BillingAddress = null
);
