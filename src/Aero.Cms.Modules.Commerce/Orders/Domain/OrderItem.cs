namespace Aero.Cms.Modules.Commerce.Orders.Domain;

/// <summary>
/// Line item within an order. Embedded value object (owned by OrderEntity in EF Core).
/// </summary>
public sealed class OrderItem : Entity
{
        /// <summary>
    /// Gets or sets the Product Id.
    /// </summary>
public long ProductId { get; set; }
        /// <summary>
    /// Gets or sets the Product Name.
    /// </summary>
public string ProductName { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Sku.
    /// </summary>
public string? Sku { get; set; }
        /// <summary>
    /// Gets or sets the Quantity.
    /// </summary>
public int Quantity { get; set; }
        /// <summary>
    /// Gets or sets the Unit Price.
    /// </summary>
public decimal UnitPrice { get; set; }
        /// <summary>
    /// Gets or sets the Total Price.
    /// </summary>
public decimal TotalPrice => UnitPrice * Quantity;
}
