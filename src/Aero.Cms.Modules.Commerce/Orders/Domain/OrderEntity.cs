namespace Aero.Cms.Modules.Commerce.Orders.Domain;

/// <summary>
/// Order aggregate root. Persisted via EF Core for relational integrity.
/// State transitions managed by <see cref="OrderStateMachine"/>.
/// </summary>
public sealed class OrderEntity : Entity
{
        /// <summary>
    /// Gets or sets the Customer Id.
    /// </summary>
public string? CustomerId { get; set; }
        /// <summary>
    /// Gets or sets the Status.
    /// </summary>
public OrderStatus Status { get; set; } = OrderStatus.Submitted;
        /// <summary>
    /// Gets or sets the Items.
    /// </summary>
public List<OrderItem> Items { get; set; } = [];
        /// <summary>
    /// Gets or sets the Total Amount.
    /// </summary>
public decimal TotalAmount => Items.Sum(i => i.TotalPrice);
        /// <summary>
    /// Gets or sets the Shipping Address.
    /// </summary>
public Address? ShippingAddress { get; set; }
        /// <summary>
    /// Gets or sets the Billing Address.
    /// </summary>
public Address? BillingAddress { get; set; }
        /// <summary>
    /// Gets or sets the Buyer Id.
    /// </summary>
public long? BuyerId { get; set; }
        /// <summary>
    /// Gets or sets the Buyer.
    /// </summary>
public Buyer? Buyer { get; set; }
        /// <summary>
    /// Gets or sets the Grace Period Expires At.
    /// </summary>
public DateTimeOffset? GracePeriodExpiresAt { get; set; }
        /// <summary>
    /// Gets or sets the Payment Reference.
    /// </summary>
public string? PaymentReference { get; set; }
}
