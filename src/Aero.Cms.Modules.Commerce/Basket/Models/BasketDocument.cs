using System.Text.Json.Serialization;
using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Commerce.Basket.Models;

/// <summary>
/// Shopping basket persisted as an AeroDB document.
/// </summary>
/// <remarks>
/// The service looks up baskets by <see cref="CustomerId"/>, but this type does not enforce uniqueness, tenant or
/// site ownership. <see cref="TotalAmount"/> is derived from the stored item snapshots and is not serialized.
/// </remarks>
public sealed class BasketDocument : SableDocument, IAuditable
{
    /// <summary>
    /// Gets or sets the customer identifier used by the basket service for lookup.
    /// </summary>
    public string CustomerId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the item snapshots currently held in the basket.
    /// </summary>
    public List<BasketItem> Items { get; set; } = [];

    /// <summary>
    /// Gets the sum of the current item totals.
    /// </summary>
    [JsonIgnore]
    public decimal TotalAmount => Items.Sum(i => i.TotalPrice);

    /// <summary>
    /// Gets or sets a currency label for the basket; it is not used when computing <see cref="TotalAmount"/>.
    /// </summary>
    public string Currency { get; set; } = "USD";

    /// <summary>Gets or sets the UTC creation timestamp assigned by the caller.</summary>
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>Gets or sets the UTC timestamp last assigned by a basket mutation.</summary>
    public DateTimeOffset? ModifiedOn { get; set; }
    /// <summary>Gets or sets the optional creator identifier; this service does not assign it.</summary>
    public string? CreatedBy { get; set; }
    /// <summary>Gets or sets the optional modifier identifier; this service does not assign it.</summary>
    public string? ModifiedBy { get; set; }
}
