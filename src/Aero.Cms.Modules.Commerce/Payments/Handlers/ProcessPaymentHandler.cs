using Aero.Cms.Modules.Commerce.Orders.Events;
using Microsoft.Extensions.Logging;
using Wolverine;
using Wolverine.Attributes;

namespace Aero.Cms.Modules.Commerce.Payments.Handlers;

/// <summary>
/// Simulates payment processing after a stock-confirmed order event.
/// </summary>
/// <remarks>
/// Each invocation waits briefly and then uses a process-local random value to publish either a success event
/// (nominally 90 percent of attempts) or a failure event. It does not contact a payment provider, load or update an
/// order, persist a payment reference, enforce idempotency, or provide a transaction with the eventual status update.
/// </remarks>
[WolverineHandler]
public sealed class ProcessPaymentHandler(
    IMessageBus bus,
    ILogger<ProcessPaymentHandler> log) : IWolverineHandler
{
    private static readonly Random _rng = new();

    /// <summary>
    /// Publishes a simulated payment outcome for a stock-confirmed order.
    /// </summary>
    /// <param name="event">The stock-confirmed order event that supplies the order identifier.</param>
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
