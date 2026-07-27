using Aero.Core.Data;
using AeroDB.Sable;
using System.Text.Json.Serialization;

namespace Aero.Cms.Modules.Commerce.Catalog.Models;

/// <summary>Describes how a canonical product is fulfilled.</summary>
public enum ProductFulfillmentMode
{
    /// <summary>A stocked physical or otherwise inventory-managed product.</summary>
    Inventory = 0,

    /// <summary>A non-inventory product purchased once.</summary>
    NonInventoryOneTime = 1,

    /// <summary>A non-inventory product whose recurring billing is owned by a payment provider.</summary>
    NonInventoryRecurring = 2
}

/// <summary>Tenant-owned canonical product and inventory record.</summary>
public sealed class ProductDocument : SableDocument, IAuditable, IVersioned
{
    public long TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Sku { get; set; } = string.Empty;
    public ProductFulfillmentMode FulfillmentMode { get; set; } = ProductFulfillmentMode.Inventory;
    /// <summary>Only these canonical products are eligible for a provider-owned recurring checkout.</summary>
    [JsonIgnore]
    public bool IsProviderRecurringEligible => FulfillmentMode == ProductFulfillmentMode.NonInventoryRecurring;
    public int StockQuantity { get; set; }
    public bool IsActive { get; set; } = true;
    public Dictionary<string, string> Attributes { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public long Version { get; set; }
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ModifiedOn { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
}
