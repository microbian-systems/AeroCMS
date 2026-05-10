using System.Linq.Expressions;
using Aero.Cms.Modules.Commerce.Orders.Domain;
using Aero.Cms.Modules.Commerce.Orders.Events;
using Aero.Cms.Modules.Commerce.Orders.Handlers;
using Aero.Cms.Modules.Commerce.Orders.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wolverine;

namespace Aero.Cms.Modules.Commerce.Orders.Api;

public static class OrderEndpoints
{
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
/// Request DTO for creating an order.
/// </summary>
public sealed record CreateOrderRequest(
    string CustomerId,
    Address ShippingAddress,
    Address? BillingAddress = null
);
