namespace Aero.Cms.Modules.Commerce.Orders.Events;

/// <summary>
/// Represents a record for OrderStatusChangedToPaid.
/// </summary>
public sealed record OrderStatusChangedToPaid(long OrderId, long TenantId, long SiteId, long ExternalMemberId);
