using Aero.Cms.Modules.Commerce.Orders.Events;
using Microsoft.Extensions.Logging;
using Wolverine;
using Wolverine.Attributes;

namespace Aero.Cms.Modules.Commerce.Payments.Handlers;

/// <summary>
/// Simulates payment processing when an order reaches StockConfirmed status.
/// Publishes OrderPaymentSucceeded or OrderPaymentFailed.
/// Replaces eShop's PaymentProcessor service.
/// </summary>
[WolverineHandler]
public sealed class ProcessPaymentHandler(
    IMessageBus bus,
    ILogger<ProcessPaymentHandler> log) : IWolverineHandler
{
    private static readonly Random _rng = new();

    public async Task Handle(OrderStatusChangedToStockConfirmed @event)
    {
        // Simulate payment processing with 90% success rate
        var paymentReference = $"PAY-{@event.OrderId}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";

        // Simulate a brief processing delay
        await Task.Delay(100);

        if (_rng.NextDouble() < 0.9)
        {
            log.LogInformation("Payment succeeded for order {OrderId}, reference: {Ref}",
                @event.OrderId, paymentReference);

            await bus.PublishAsync(new OrderPaymentSucceeded(@event.OrderId, paymentReference));
        }
        else
        {
            log.LogWarning("Payment FAILED for order {OrderId}", @event.OrderId);

            await bus.PublishAsync(new OrderPaymentFailed(@event.OrderId, "Simulated payment failure"));
        }
    }
}
