namespace Aero.Cms.Modules.Commerce.Basket.Models;

/// <summary>
/// Line item within a basket. Embedded value object (no independent identity).
/// </summary>
public sealed record BasketItem
{
    public long ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string? Sku { get; init; }
    public string? ImageUrl { get; init; }
    public int Quantity { get; init; } = 1;
    public decimal UnitPrice { get; init; }

    public decimal TotalPrice => UnitPrice * Quantity;
}
