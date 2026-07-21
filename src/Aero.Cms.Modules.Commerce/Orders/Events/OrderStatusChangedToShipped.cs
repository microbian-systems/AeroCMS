namespace Aero.Cms.Modules.Commerce.Orders.Events;

/// <summary>
/// Represents a record for OrderStatusChangedToShipped.
/// </summary>
public sealed record OrderStatusChangedToShipped(long OrderId, long TenantId, long SiteId, long ExternalMemberId);
