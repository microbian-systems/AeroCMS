namespace Aero.Cms.Modules.Commerce.Orders.Events;

/// <summary>
/// Represents a record for OrderStatusChangedToSubmitted.
/// </summary>
public sealed record OrderStatusChangedToSubmitted(long OrderId, long TenantId, long SiteId, long ExternalMemberId, decimal TotalAmount);
