namespace Aero.Cms.Modules.Commerce.Orders.Domain;

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
    public decimal TotalPrice => UnitPrice * Quantity;
}
