namespace Aero.Cms.Modules.Commerce.Orders.Domain;

/// <summary>
/// Order aggregate root. Persisted via EF Core for relational integrity.
/// State transitions managed by <see cref="OrderStateMachine"/>.
/// </summary>
public sealed class OrderEntity : Entity
{
    public string? CustomerId { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Submitted;
    public List<OrderItem> Items { get; set; } = [];
    public decimal TotalAmount => Items.Sum(i => i.TotalPrice);
    public Address? ShippingAddress { get; set; }
    public Address? BillingAddress { get; set; }
    public long? BuyerId { get; set; }
    public Buyer? Buyer { get; set; }
    public DateTimeOffset? GracePeriodExpiresAt { get; set; }
    public string? PaymentReference { get; set; }
}
