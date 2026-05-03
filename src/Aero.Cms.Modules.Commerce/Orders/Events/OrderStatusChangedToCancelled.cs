namespace Aero.Cms.Modules.Commerce.Orders.Events;

public sealed record OrderStatusChangedToCancelled(long OrderId, string Reason);
