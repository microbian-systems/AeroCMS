namespace Aero.Cms.Modules.Commerce.Orders.Domain;

/// <summary>
/// Order aggregate state machine lifecycle.
/// Submitted ─► AwaitingValidation ─► StockConfirmed ─► Paid ─► Shipped
///     │                │                       │              │
///     └──Cancelled←────┘                       └──Cancelled←──┘
/// </summary>
public enum OrderStatus
{
    Submitted,
    AwaitingValidation,
    StockConfirmed,
    Paid,
    Shipped,
    Cancelled
}
