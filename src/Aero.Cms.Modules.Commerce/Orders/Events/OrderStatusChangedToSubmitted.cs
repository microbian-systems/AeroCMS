using Aero.Cms.Modules.Commerce.Orders.Domain;

namespace Aero.Cms.Modules.Commerce.Orders.Events;

public sealed record OrderStatusChangedToSubmitted(long OrderId, string CustomerId, decimal TotalAmount);
