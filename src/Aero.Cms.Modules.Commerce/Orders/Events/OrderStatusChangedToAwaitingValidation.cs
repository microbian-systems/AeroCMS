namespace Aero.Cms.Modules.Commerce.Orders.Events;

/// <summary>
/// Represents a record for OrderStatusChangedToAwaitingValidation.
/// </summary>
public sealed record OrderStatusChangedToAwaitingValidation(long OrderId, long TenantId, long SiteId, long ExternalMemberId);
