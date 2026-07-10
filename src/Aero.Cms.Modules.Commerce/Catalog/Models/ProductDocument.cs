namespace Aero.Cms.Modules.Commerce.Catalog.Models;

/// <summary>
/// Catalog product stored as an AeroDB document.
/// Content type integration for product catalog.
/// </summary>
public sealed class ProductDocument : Entity
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
    /// Gets or sets the Price.
    /// </summary>
public decimal Price { get; set; }
        /// <summary>
    /// Gets or sets the Compare At Price.
    /// </summary>
public decimal? CompareAtPrice { get; set; }
        /// <summary>
    /// Gets or sets the Currency.
    /// </summary>
public string Currency { get; set; } = "USD";
        /// <summary>
    /// Gets or sets the Stock Quantity.
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
}
