using Aero.Cms.Modules.Commerce.Catalog.Events;
using Aero.Cms.Modules.Commerce.Catalog.Models;
using Aero.Cms.Modules.Commerce.Catalog.Services;
using Aero.Cms.Modules.Commerce.Orders.Events;
using Aero.Cms.Modules.Commerce.Orders.Services;
using Microsoft.Extensions.Logging;
using Wolverine;
using Wolverine.Attributes;

namespace Aero.Cms.Modules.Commerce.Catalog.Handlers;

/// <summary>
/// Validates stock when an order enters AwaitingValidation.
/// Publishes OrderStockConfirmed if all items are in stock,
/// or OrderStockRejected if any are out of stock.
/// Replaces eShop's OrderStatusChangedToAwaitingValidationIntegrationEvent → Catalog.API flow.
/// </summary>
[WolverineHandler]
public sealed class StockValidationHandler(
    IProductService productService,
    IOrderService orderService,
    IMessageBus bus,
    ILogger<StockValidationHandler> log) : IWolverineHandler
{
    public async Task Handle(OrderStatusChangedToAwaitingValidation @event)
    {
        try
        {
            var order = await orderService.FindByIdAsync(@event.OrderId);
            var outOfStock = new List<long>();

            foreach (var item in order.Items)
            {
                var product = await productService.FindByIdAsync(item.ProductId);
                if (product is null || product.StockQuantity < item.Quantity)
                {
                    outOfStock.Add(item.ProductId);
                    log.LogWarning("Product {ProductId} is out of stock (requested: {Requested}, available: {Available})",
                        item.ProductId, item.Quantity, product?.StockQuantity ?? 0);
                }
            }

            if (outOfStock.Count == 0)
            {
                await bus.PublishAsync(new OrderStockConfirmed(@event.OrderId));
                log.LogInformation("Stock confirmed for order {OrderId}", @event.OrderId);
            }
            else
            {
                await bus.PublishAsync(new OrderStockRejected(@event.OrderId, outOfStock));
                log.LogWarning("Stock rejected for order {OrderId}: items {Items} out of stock",
                    @event.OrderId, string.Join(", ", outOfStock));
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Stock validation failed for order {OrderId}", @event.OrderId);
        }
    }
}
