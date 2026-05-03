using Aero.Cms.Modules.Commerce.Orders.Domain;

namespace Aero.Cms.Modules.Commerce.Orders.Services;

public interface IOrderService : IGenericEntityFrameworkRepository<OrderEntity>
{
    Task<Result<OrderEntity?, AeroError>> FindByCustomerAsync(string customerId, CancellationToken ct = default);
}
