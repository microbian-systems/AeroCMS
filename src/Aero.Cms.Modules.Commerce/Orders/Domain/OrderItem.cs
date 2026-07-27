using Aero.Cms.Modules.Commerce.Catalog.Models;

namespace Aero.Cms.Modules.Commerce.Orders.Domain;

/// <summary>Commercial collection authority captured when an order is created.</summary>
public enum OrderBillingKind { OneTime = 0, Recurring = 1 }

/// <summary>Immutable canonical-product and listing-price snapshot retained by an order.</summary>
public sealed class OrderItem
{
    public long ListingId { get; set; }
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string Currency { get; set; } = "USD";
    public OrderBillingKind BillingKind { get; set; }
    public ProductFulfillmentMode FulfillmentMode { get; set; }
    public int? BillingIntervalDays { get; set; }
    /// <summary>Provider-owned offer references, never credentials.</summary>
    public string? StripePriceId { get; set; }
    public string? PayPalPlanId { get; set; }
    public decimal TotalPrice => UnitPrice * Quantity;
}
