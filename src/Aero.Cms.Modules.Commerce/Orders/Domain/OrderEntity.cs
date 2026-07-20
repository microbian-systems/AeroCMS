namespace Aero.Cms.Modules.Commerce.Orders.Domain;

/// <summary>
/// Order aggregate root persisted by the commerce module through EF Core.
/// </summary>
/// <remarks>
/// Status changes are validated by <see cref="Aero.Cms.Modules.Commerce.Shared.StateMachine.OrderStateMachine"/>;
/// persistence and related business operations remain the responsibility of the calling workflow.
/// </remarks>
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
        /// Gets the sum of the current line-item totals.
/// </summary>
/// <remarks>This computed value does not add tax, shipping, discounts, currency conversion, or payment fees.</remarks>
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
