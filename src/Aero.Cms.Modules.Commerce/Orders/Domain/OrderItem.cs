namespace Aero.Cms.Modules.Commerce.Orders.Domain;

/// <summary>
/// Line item within an order. Embedded value object (owned by OrderEntity in EF Core).
/// </summary>
public sealed class OrderItem : Entity
{
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice => UnitPrice * Quantity;
}
