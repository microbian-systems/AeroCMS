using Aero.Cms.Modules.Commerce.Basket.Services;
using Aero.Cms.Modules.Commerce.Orders.Events;
using Microsoft.Extensions.Logging;
using Wolverine;
using Wolverine.Attributes;

namespace Aero.Cms.Modules.Commerce.Basket.Handlers;

/// <summary>
/// When an order is started, clear the customer's basket.
/// This replaces eShop's OrderStartedIntegrationEvent → Basket.API flow.
/// </summary>
[WolverineHandler]
public sealed class ClearBasketOnOrderHandler(
    IBasketService basketService,
    ILogger<ClearBasketOnOrderHandler> log) : IWolverineHandler
{
        /// <summary>
    /// Handle method.
    /// </summary>
public async Task Handle(OrderStarted @event)
    {
        var result = await basketService.ClearBasketAsync(@event.CustomerId);

        if (result.IsSuccess)
        {
            log.LogInformation("Cleared basket for customer {CustomerId} after order {OrderId}",
                @event.CustomerId, @event.OrderId);
        }
        else
        {
            log.LogWarning("Failed to clear basket for customer {CustomerId}", @event.CustomerId);
        }
    }
}
