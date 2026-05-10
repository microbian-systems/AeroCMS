using System.Linq.Expressions;
using Aero.Cms.Modules.Commerce.Orders.Domain;
using Aero.Cms.Modules.Commerce.Orders.Events;
using Aero.Cms.Modules.Commerce.Orders.Services;
using Microsoft.Extensions.Logging;
using TickerQ.Utilities.Base;
using Wolverine;

namespace Aero.Cms.Modules.Commerce.Jobs;

/// <summary>
/// TickerQ background job that checks for orders past their grace period.
/// Replaces eShop's OrderProcessor polling service.
/// Runs every minute via cron expression.
/// </summary>
public sealed class GracePeriodJob(
    IOrderService orderService,
    IMessageBus bus,
    ILogger<GracePeriodJob> log)
{
    [TickerFunction("commerce.grace-period")]
    public async Task CheckExpiredOrders(
        TickerFunctionContext context,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        Expression<Func<OrderEntity, bool>> predicate = o =>
            o.Status == OrderStatus.Submitted &&
            o.GracePeriodExpiresAt <= now;

        var orders = await orderService.FindAsync(predicate);

        foreach (var order in orders)
        {
            await bus.PublishAsync(new GracePeriodConfirmed(order.Id));
            log.LogInformation("Grace period expired for order {OrderId}", order.Id);
        }

        if (orders.Any())
        {
            log.LogInformation("Processed {Count} expired grace period orders", orders.Count());
        }
    }
}
