namespace Aero.Cms.Modules.Commerce.Basket.Models;

/// <summary>
/// Line item within a basket. Embedded value object (no independent identity).
/// </summary>
public sealed record BasketItem
{
        /// <summary>
    /// Gets or sets the Product Id.
    /// </summary>
public long ProductId { get; init; }
        /// <summary>
    /// Gets or sets the Product Name.
    /// </summary>
public string ProductName { get; init; } = string.Empty;
        /// <summary>
    /// Gets or sets the Sku.
    /// </summary>
public string? Sku { get; init; }
        /// <summary>
    /// Gets or sets the Image Url.
    /// </summary>
public string? ImageUrl { get; init; }
        /// <summary>
    /// Gets or sets the Quantity.
    /// </summary>
public int Quantity { get; init; } = 1;
        /// <summary>
    /// Gets or sets the Unit Price.
    /// </summary>
public decimal UnitPrice { get; init; }

        /// <summary>
    /// Gets or sets the Total Price.
    /// </summary>
public decimal TotalPrice => UnitPrice * Quantity;
}
