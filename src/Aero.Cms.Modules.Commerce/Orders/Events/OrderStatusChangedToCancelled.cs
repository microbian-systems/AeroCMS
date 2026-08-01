namespace Aero.Cms.Modules.Commerce.Orders.Events;

/// <summary>
/// Represents a record for OrderStatusChangedToCancelled.
/// </summary>
public sealed record OrderStatusChangedToCancelled(long OrderId, long TenantId, long SiteId, long ExternalMemberId, string Reason);
