using Aero.Cms.Modules.Commerce.Orders.Data;
using Aero.Cms.Modules.Commerce.Orders.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ef = Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions;

namespace Aero.Cms.Modules.Commerce.Orders.Services;

/// <summary>
/// Represents a class for OrderService.
/// </summary>
public sealed class OrderService : GenericEntityFrameworkRepository<OrderEntity>, IOrderService
{
    private readonly CommerceDbContext _commerceContext;

        /// <summary>
    /// Initializes a new instance of the <see cref="OrderService"/> class.
    /// </summary>
public OrderService(CommerceDbContext context, ILogger<OrderService> log)
        : base(context, log)
    {
        _commerceContext = context;
    }

        /// <summary>
    /// FindByCustomerAsync method.
    /// </summary>
public async Task<Result<OrderEntity?, AeroError>> FindByCustomerAsync(string customerId, CancellationToken ct = default)
    {
        try
        {
            var query = _commerceContext.Orders.Include(o => o.Items).Where(o => o.CustomerId == customerId);
            var order = await Ef.FirstOrDefaultAsync(query, ct);

            return order is null
                ? Prelude.Fail<OrderEntity?, AeroError>(AeroError.CreateError($"Order for customer '{customerId}' not found"))
                : Prelude.Ok<OrderEntity?, AeroError>(order);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<OrderEntity?, AeroError>(AeroError.CreateError(ex.Message));
        }
    }
}
