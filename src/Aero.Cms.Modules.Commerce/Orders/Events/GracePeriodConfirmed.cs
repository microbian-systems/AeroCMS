namespace Aero.Cms.Modules.Commerce.Orders.Events;

/// <summary>
/// Published when the grace period for an order has expired.
/// Moves the order from Submitted → AwaitingValidation.
/// </summary>
public sealed record GracePeriodConfirmed(long OrderId);
