namespace Aero.Cms.Modules.Commerce.Orders.Domain;

/// <summary>Payment lifecycle independent from fulfillment and operational order status.</summary>
public enum OrderPaymentStatus
{
    Unpaid,
    Pending,
    Paid,
    Failed,
    Cancelled,
    ManualReview
}
