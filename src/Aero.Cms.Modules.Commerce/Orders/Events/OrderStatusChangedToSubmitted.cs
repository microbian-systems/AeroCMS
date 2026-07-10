namespace Aero.Cms.Modules.Commerce.Orders.Events;

/// <summary>
/// Represents a record for OrderStatusChangedToSubmitted.
/// </summary>
public sealed record OrderStatusChangedToSubmitted(long OrderId, string CustomerId, decimal TotalAmount);
