using Aero.Cms.Modules.Commerce.Basket.Services;
using Aero.Cms.Modules.Commerce.Orders.Domain;
using Aero.Cms.Modules.Commerce.Orders.Events;
using Aero.Cms.Modules.Commerce.Orders.Services;
using Aero.Cms.Modules.Commerce.Orders.Validation;
using Microsoft.Extensions.Logging;
using Wolverine;
using Wolverine.Attributes;

namespace Aero.Cms.Modules.Commerce.Orders.Handlers;

/// <summary>
/// Handles order creation from a customer's basket.
/// Replaces eShop's CreateOrderCommandHandler (MediatR → Wolverine).
/// Flow: validate basket → create order → save → publish events.
/// </summary>
[WolverineHandler]
public sealed class CreateOrderHandler(
    IOrderService orderService,
    IBasketService basketService,
    IMessageBus bus,
    ILogger<CreateOrderHandler> log) : IWolverineHandler
{
        /// <summary>
    /// Handle method.
    /// </summary>
public async Task Handle(CreateOrder command)
    {
        // 1. Load the customer's basket
        var basketResult = await basketService.GetOrCreateBasketAsync(command.CustomerId);
        if (basketResult is not Result<Basket.Models.BasketDocument, AeroError>.Ok(var basket) || basket.Items.Count == 0)
        {
            log.LogWarning("Cannot create order for customer {CustomerId}: basket is empty", command.CustomerId);
            return;
        }

        // 2. Build order from basket
        var order = new OrderEntity
        {
            Id = Snowflake.NewId(),
            CustomerId = command.CustomerId,
            Status = OrderStatus.Submitted,
            ShippingAddress = command.ShippingAddress,
            BillingAddress = command.BillingAddress ?? command.ShippingAddress,
            GracePeriodExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5), // eShop default: 5 min grace period
            CreatedOn = DateTimeOffset.UtcNow
        };

        foreach (var item in basket.Items)
        {
            order.Items.Add(new OrderItem
            {
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                Sku = item.Sku,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice
            });
        }

        // 3. Validate
        var validator = new CreateOrderValidator();
        var validation = await validator.ValidateAsync(order);
        if (!validation.IsValid)
        {
            log.LogWarning("Order validation failed for customer {CustomerId}: {Errors}",
                command.CustomerId, string.Join(", ", validation.Errors.Select(e => e.ErrorMessage)));
            return;
        }

        // 4. Save order
        await orderService.InsertAsync(order);

        // 5. Clear basket
        await basketService.ClearBasketAsync(command.CustomerId);

        // 6. Publish events
        await bus.PublishAsync(new OrderStarted(order.Id, order.CustomerId));
        await bus.PublishAsync(new OrderStatusChangedToSubmitted(order.Id, order.CustomerId, order.TotalAmount));

        log.LogInformation("Order {OrderId} created for customer {CustomerId} with {ItemCount} items",
            order.Id, order.CustomerId, order.Items.Count);
    }
}

/// <summary>
/// Command to create an order from a customer's basket.
/// </summary>
public sealed record CreateOrder(
    string CustomerId,
    Address? ShippingAddress,
    Address? BillingAddress = null
);
