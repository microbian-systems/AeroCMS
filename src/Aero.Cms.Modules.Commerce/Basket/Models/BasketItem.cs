namespace Aero.Cms.Modules.Commerce.Basket.Models;

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
    public decimal TotalPrice => UnitPrice * Quantity;
}
