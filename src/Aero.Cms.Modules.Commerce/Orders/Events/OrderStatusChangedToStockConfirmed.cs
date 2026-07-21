namespace Aero.Cms.Modules.Commerce.Orders.Events;

/// <summary>
/// Represents a record for OrderStatusChangedToStockConfirmed.
/// </summary>
public sealed record OrderStatusChangedToStockConfirmed(long OrderId, long TenantId, long SiteId, long ExternalMemberId);
