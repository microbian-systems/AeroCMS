using System.Text.Json.Serialization;
using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Commerce.Basket.Models;

/// <summary>External-member basket isolated by tenant and storefront site.</summary>
public sealed class BasketDocument : SableDocument, IAuditable, IVersioned
{
    public long TenantId { get; set; }
    public long SiteId { get; set; }
    public long ExternalMemberId { get; set; }
    public string Currency { get; set; } = "USD";
    public List<BasketItem> Items { get; set; } = [];
    public long Version { get; set; }
    [JsonIgnore] public decimal TotalAmount => Items.Sum(i => i.TotalPrice);
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ModifiedOn { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
}
