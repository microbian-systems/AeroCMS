namespace Aero.Cms.Modules.Commerce.Orders.Domain;

/// <summary>
/// Identifies the order lifecycle state evaluated by <see cref="Aero.Cms.Modules.Commerce.Shared.StateMachine.OrderStateMachine"/>.
/// </summary>
public enum OrderStatus
{
    /// <summary>The initial state for an order awaiting the validation workflow.</summary>
    Submitted,
    /// <summary>The order is awaiting validation before stock confirmation.</summary>
    AwaitingValidation,
    /// <summary>The order has reached the stock-confirmed state.</summary>
    StockConfirmed,
    /// <summary>The order has reached the paid state.</summary>
    Paid,
    /// <summary>The order has reached the shipped state.</summary>
    Shipped,
    /// <summary>The order was cancelled before it reached the paid state.</summary>
    Cancelled
}
