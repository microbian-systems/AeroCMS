namespace Aero.Cms.Modules.Commerce.Basket.Models;

/// <summary>
/// Shopping cart stored as a Marten document.
/// One basket per customer — keyed by customer identity ID.
/// </summary>
public sealed class BasketDocument : Entity
{
    /// <summary>
    /// The identity ID of the customer who owns this basket.
    /// </summary>
    public string CustomerId { get; set; } = string.Empty;

    /// <summary>
    /// Line items in the basket.
    /// </summary>
    public List<BasketItem> Items { get; set; } = [];

    /// <summary>
    /// Computed total price of all items.
    /// </summary>
    public decimal TotalAmount => Items.Sum(i => i.TotalPrice);

    /// <summary>
    /// ISO currency code (e.g. USD, EUR).
    /// </summary>
    public string Currency { get; set; } = "USD";
}
