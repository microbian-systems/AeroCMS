namespace Aero.Cms.Modules.Commerce.Orders.Events;

/// <summary>
/// Published when an order is initiated. Triggers basket clearance.
/// </summary>
public sealed record OrderStarted(long OrderId, string CustomerId);
