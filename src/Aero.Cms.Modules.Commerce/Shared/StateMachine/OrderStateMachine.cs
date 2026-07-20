using Aero.Cms.Modules.Commerce.Orders.Domain;

namespace Aero.Cms.Modules.Commerce.Shared.StateMachine;

/// <summary>
/// Validates and applies the supported in-memory status transitions for an order.
/// </summary>
/// <remarks>
/// This state machine only changes <see cref="OrderEntity.Status"/> on the supplied instance. It does not persist
/// the order, reserve or release stock, capture a payment, publish an event, authorize a caller, or coordinate
/// concurrent updates. The caller must perform those responsibilities after a successful result where applicable.
/// </remarks>
public static class OrderStateMachine
{
    /// <summary>
    /// Attempts a supported transition and updates the supplied order when it is valid.
    /// </summary>
    /// <param name="order">The order whose current status determines whether the requested transition is allowed.</param>
    /// <param name="newStatus">The requested target status.</param>
    /// <returns>
    /// A successful result containing the same, now-updated <paramref name="order"/> for one of the allowed
    /// transitions; otherwise a failed result and the order remains unchanged.
    /// </returns>
    /// <remarks>
    /// The allowed forward transitions are Submitted to AwaitingValidation, AwaitingValidation to StockConfirmed,
    /// StockConfirmed to Paid, and Paid to Shipped. Cancellation is allowed only from Submitted,
    /// AwaitingValidation, or StockConfirmed.
    /// </remarks>
    public static Result<OrderEntity, AeroError> Transition(OrderEntity order, OrderStatus newStatus)
    {
        var target = newStatus;

        return (order.Status, target) switch
        {
            (OrderStatus.Submitted, OrderStatus.AwaitingValidation) => ApplyTransition(order, target),
            (OrderStatus.AwaitingValidation, OrderStatus.StockConfirmed) => ApplyTransition(order, target),
            (OrderStatus.StockConfirmed, OrderStatus.Paid) => ApplyTransition(order, target),
            (OrderStatus.Paid, OrderStatus.Shipped) => ApplyTransition(order, target),

            (OrderStatus.Submitted, OrderStatus.Cancelled) => ApplyTransition(order, target),
            (OrderStatus.AwaitingValidation, OrderStatus.Cancelled) => ApplyTransition(order, target),
            (OrderStatus.StockConfirmed, OrderStatus.Cancelled) => ApplyTransition(order, target),

            _ => Prelude.Fail<OrderEntity, AeroError>(
                AeroError.CreateError($"Invalid order state transition: {order.Status} → {target}"))
        };
    }

    private static Result<OrderEntity, AeroError> ApplyTransition(OrderEntity order, OrderStatus newStatus)
    {
        order.Status = newStatus;
        return Prelude.Ok<OrderEntity, AeroError>(order);
    }
}
