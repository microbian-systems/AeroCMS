namespace Aero.Cms.Modules.Commerce.Orders.Events;

/// <summary>
/// Published when payment processing fails.
/// Orders may be retried or cancelled.
/// </summary>
public sealed record OrderPaymentFailed(long OrderId, string Reason);
