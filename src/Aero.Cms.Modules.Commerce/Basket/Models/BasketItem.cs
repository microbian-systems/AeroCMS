namespace Aero.Cms.Modules.Commerce.Basket.Models;

/// <summary>Billing authority captured from a currently published listing.</summary>
public enum BasketBillingKind
{
    OneTime = 0,
    Recurring = 1
}

/// <summary>
/// Immutable merchant plan bindings resolved from a recurring listing. These are provider
/// identifiers only; credentials, customer IDs, and checkout state are never placed in a basket.
/// </summary>
public sealed record BasketSubscriptionOfferSnapshot
{
    public int IntervalDays { get; init; }
    public string? StripePriceId { get; init; }
    public string? PayPalPlanId { get; init; }
}

/// <summary>Authoritative storefront snapshot captured when a listing is added.</summary>
public sealed record BasketItem
{
    public long ListingId { get; init; }
    public long ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string Sku { get; init; } = string.Empty;
    public string? ImageUrl { get; init; }
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public string Currency { get; init; } = "USD";
    public BasketBillingKind BillingKind { get; init; }
    public int? BillingIntervalDays { get; init; }
    public BasketSubscriptionOfferSnapshot? SubscriptionOffer { get; init; }
    public decimal TotalPrice => UnitPrice * Quantity;
}
