namespace Aero.Cms.Modules.Commerce.Orders.Events;

/// <summary>
/// Published when payment processing succeeds.
/// Moves order from StockConfirmed → Paid.
/// </summary>
public sealed record OrderPaymentSucceeded(long OrderId, string PaymentReference);
