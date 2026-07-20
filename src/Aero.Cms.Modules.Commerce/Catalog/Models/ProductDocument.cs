using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Commerce.Catalog.Models;

/// <summary>
/// Catalog product persisted as an AeroDB document.
/// </summary>
/// <remarks>
/// Price and stock are stored scalar values. This model does not itself validate currency codes, enforce a unique
/// SKU or slug, calculate discounts or tax, reserve stock, or provide optimistic concurrency.
/// </remarks>
public sealed class ProductDocument : SableDocument, IAuditable
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public string Name { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Slug.
    /// </summary>
public string Slug { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Sku.
    /// </summary>
public string? Sku { get; set; }
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description { get; set; }
        /// <summary>
    /// Gets or sets the Short Description.
    /// </summary>
public string? ShortDescription { get; set; }
        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public string? Category { get; set; }
        /// <summary>
    /// Gets or sets the Image Url.
    /// </summary>
public string? ImageUrl { get; set; }
        /// <summary>
    /// Gets or sets the catalog price; no rounding, tax, or currency conversion is applied by this model.
    /// </summary>
public decimal Price { get; set; }
        /// <summary>
    /// Gets or sets the optional comparison price; this model does not require it to exceed <see cref="Price"/>.
    /// </summary>
public decimal? CompareAtPrice { get; set; }
        /// <summary>
    /// Gets or sets the currency label for the price; this model does not validate it.
    /// </summary>
public string Currency { get; set; } = "USD";
        /// <summary>
    /// Gets or sets the catalog stock quantity; setting it does not reserve or allocate stock.
    /// </summary>
public int StockQuantity { get; set; }
        /// <summary>
    /// Gets or sets the Is Published.
    /// </summary>
public bool IsPublished { get; set; }
        /// <summary>
    /// Gets or sets the Is Featured.
    /// </summary>
public bool IsFeatured { get; set; }
        /// <summary>
    /// Gets or sets the Attributes.
    /// </summary>
public Dictionary<string, string> Attributes { get; set; } = [];
        /// <summary>
    /// Gets or sets the Tags.
    /// </summary>
    public List<string> Tags { get; set; } = [];

    // IAuditable
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ModifiedOn { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
}
