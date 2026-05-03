using Aero.Cms.Modules.Commerce.Catalog.Events;
using Aero.Cms.Modules.Commerce.Orders.Domain;
using Aero.Cms.Modules.Commerce.Orders.Events;
using Aero.Cms.Modules.Commerce.Orders.Services;
using Aero.Cms.Modules.Commerce.Shared.StateMachine;
using Microsoft.Extensions.Logging;
using Wolverine;
using Wolverine.Attributes;

namespace Aero.Cms.Modules.Commerce.Orders.Handlers;

/// <summary>
/// Wolverine handlers for order status transitions.
/// Each handler validates the transition via OrderStateMachine,
/// persists the change, and publishes the resulting event.
/// </summary>
public static class OrderStatusHandlers
{
    /// <summary>
    /// Grace period expired → move from Submitted to AwaitingValidation.
    /// </summary>
    [WolverineHandler]
    public static async Task Handle(
        GracePeriodConfirmed @event,
        IOrderService orderService,
        IMessageBus bus,
        ILogger<IWolverineHandler> log)
    {
        var order = await orderService.FindByIdAsync(@event.OrderId);
        if (order is null)
        {
            log.LogWarning("Order {OrderId} not found for grace period confirmation", @event.OrderId);
            return;
        }

        var result = OrderStateMachine.Transition(order, OrderStatus.AwaitingValidation);
        if (!result.IsSuccess)
        {
            log.LogWarning("Cannot transition order {OrderId} to AwaitingValidation: {Error}",
                @event.OrderId, result.ToString());
            return;
        }

        await orderService.UpdateAsync(order);
        await bus.PublishAsync(new OrderStatusChangedToAwaitingValidation(order.Id));

        log.LogInformation("Order {OrderId} moved to AwaitingValidation", order.Id);
    }

    /// <summary>
    /// Stock confirmed → move from AwaitingValidation to StockConfirmed.
    /// </summary>
    [WolverineHandler]
    public static async Task Handle(
        OrderStockConfirmed @event,
        IOrderService orderService,
        IMessageBus bus,
        ILogger<IWolverineHandler> log)
    {
        var order = await orderService.FindByIdAsync(@event.OrderId);
        if (order is null) return;

        var result = OrderStateMachine.Transition(order, OrderStatus.StockConfirmed);
        if (!result.IsSuccess) return;

        await orderService.UpdateAsync(order);
        await bus.PublishAsync(new OrderStatusChangedToStockConfirmed(order.Id));
    }

    /// <summary>
    /// Stock rejected → cancel the order.
    /// </summary>
    [WolverineHandler]
    public static async Task Handle(
        OrderStockRejected @event,
        IOrderService orderService,
        IMessageBus bus,
        ILogger<IWolverineHandler> log)
    {
        var order = await orderService.FindByIdAsync(@event.OrderId);
        if (order is null) return;

        var result = OrderStateMachine.Transition(order, OrderStatus.Cancelled);
        if (!result.IsSuccess) return;

        await orderService.UpdateAsync(order);
        await bus.PublishAsync(new OrderStatusChangedToCancelled(order.Id,
            $"Items out of stock: {string.Join(", ", @event.OutOfStockProductIds)}"));
    }

    /// <summary>
    /// Payment succeeded → move from StockConfirmed to Paid.
    /// Called from Payments slice (Phase 4).
    /// </summary>
    [WolverineHandler]
    public static async Task Handle(
        OrderPaymentSucceeded @event,
        IOrderService orderService,
        IMessageBus bus,
        ILogger<IWolverineHandler> log)
    {
        var order = await orderService.FindByIdAsync(@event.OrderId);
        if (order is null) return;

        var result = OrderStateMachine.Transition(order, OrderStatus.Paid);
        if (!result.IsSuccess) return;

        order.PaymentReference = @event.PaymentReference;
        await orderService.UpdateAsync(order);
        await bus.PublishAsync(new OrderStatusChangedToPaid(order.Id));
    }
}
