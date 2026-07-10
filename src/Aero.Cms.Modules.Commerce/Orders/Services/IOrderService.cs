using Aero.Cms.Modules.Commerce.Orders.Domain;

namespace Aero.Cms.Modules.Commerce.Orders.Services;

/// <summary>
/// Defines an interface for IOrderService.
/// </summary>
public interface IOrderService : IGenericEntityFrameworkRepository<OrderEntity>
{
        /// <summary>
    /// FindByCustomerAsync method.
    /// </summary>
Task<Result<OrderEntity?, AeroError>> FindByCustomerAsync(string customerId, CancellationToken ct = default);
}
