using Aero.Cms.Modules.Commerce.Orders.Domain;
using Aero.Cms.Modules.Commerce.Orders.Events;
using Aero.Cms.Modules.Commerce.Orders.Services;
using Wolverine;
using Wolverine.Attributes;

namespace Aero.Cms.Modules.Commerce.Orders.Handlers;

/// <summary>Advances scoped workflow state without performing stock reservation a second time.</summary>
public static class OrderStatusHandlers
{
    [WolverineHandler]
    public static async Task Handle(GracePeriodConfirmed @event, IOrderService orders, IMessageBus bus)
    {
        var result = await orders.TransitionAsync(@event.TenantId, @event.SiteId, @event.OrderId, OrderStatus.AwaitingValidation);
        if (result is Result<OrderEntity, AeroError>.Ok(var order))
            await bus.PublishAsync(new OrderStatusChangedToAwaitingValidation(order.Id, order.TenantId, order.SiteId, order.ExternalMemberId));
    }
}
