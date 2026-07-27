using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Commerce.Catalog.Models;

/// <summary>Site and culture-specific storefront merchandising for a canonical product.</summary>
public sealed class ProductListingDocument : SableDocument, IAuditable, IVersioned
{
    public long TenantId { get; set; }
    public long SiteId { get; set; }
    public long ProductId { get; set; }
    public string Culture { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ShortDescription { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? ImageUrl { get; set; }
    public decimal Price { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public string Currency { get; set; } = "USD";
    public bool IsPublished { get; set; }
    public bool IsFeatured { get; set; }
    public bool IncludeInSearch { get; set; } = true;
    public bool IncludeInPublicAi { get; set; }
    public long Version { get; set; }
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ModifiedOn { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
}
