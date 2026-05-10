using Aero.Cms.Modules.Commerce.Orders.Domain;

namespace Aero.Cms.Modules.Commerce.Shared.StateMachine;

/// <summary>
/// Railway-Oriented state transition engine for the Order aggregate.
/// Each transition validates legality before mutating state, returning
/// <see cref="Result{T,TError}"/> for composable pipeline handling.
/// </summary>
public static class OrderStateMachine
{
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
