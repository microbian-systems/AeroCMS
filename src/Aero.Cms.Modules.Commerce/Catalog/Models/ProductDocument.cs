using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Commerce.Catalog.Models;

/// <summary>Tenant-owned canonical product and inventory record.</summary>
public sealed class ProductDocument : SableDocument, IAuditable, IVersioned
{
    public long TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Sku { get; set; } = string.Empty;
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
