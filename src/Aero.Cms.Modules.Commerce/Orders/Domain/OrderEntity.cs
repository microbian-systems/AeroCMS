using Aero.Core.Data;
using AeroDB.Sable;
using System.Text.Json.Serialization;

namespace Aero.Cms.Modules.Commerce.Orders.Domain;

/// <summary>Immutable customer order snapshot owned by one external member and storefront site.</summary>
public sealed class OrderEntity : SableDocument, IAuditable, IVersioned
{
    public long TenantId { get; set; }
    public long SiteId { get; set; }
    public long ExternalMemberId { get; set; }
    public string Currency { get; set; } = "USD";
    public OrderStatus Status { get; set; } = OrderStatus.Submitted;
    public OrderPaymentStatus PaymentStatus { get; set; } = OrderPaymentStatus.Unpaid;
    public List<OrderItem> Items { get; set; } = [];
    [JsonIgnore] public decimal TotalAmount => Items.Sum(i => i.TotalPrice);
    public Address? ShippingAddress { get; set; }
    public Address? BillingAddress { get; set; }
    public Buyer? Buyer { get; set; }
    public DateTimeOffset? GracePeriodExpiresAt { get; set; }
    public long Version { get; set; }
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ModifiedOn { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
}
