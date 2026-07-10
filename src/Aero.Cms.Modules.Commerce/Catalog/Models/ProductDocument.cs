namespace Aero.Cms.Modules.Commerce.Catalog.Models;

/// <summary>
/// Catalog product stored as an AeroDB document.
/// Content type integration for product catalog.
/// </summary>
public sealed class ProductDocument : Entity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public string? Description { get; set; }
    public string? ShortDescription { get; set; }
    public string? Category { get; set; }
    public string? ImageUrl { get; set; }
    public decimal Price { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public string Currency { get; set; } = "USD";
    public int StockQuantity { get; set; }
    public bool IsPublished { get; set; }
    public bool IsFeatured { get; set; }
    public Dictionary<string, string> Attributes { get; set; } = [];
    public List<string> Tags { get; set; } = [];
}
