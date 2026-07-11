using System.Linq.Expressions;
using Aero.Cms.Modules.Commerce.Orders.Domain;
using Aero.Core;

namespace Aero.Cms.Modules.Commerce.Orders.Services;

/// <summary>
/// Repository interface for order persistence via AeroDB.Sable.
/// </summary>
public interface IOrderService
{
    /// <summary>
    /// FindByIdAsync method.
    /// </summary>
    Task<OrderEntity?> FindByIdAsync(long id);

    /// <summary>
    /// FindAsync method — expression-based query.
    /// </summary>
    Task<IEnumerable<OrderEntity>> FindAsync(Expression<Func<OrderEntity, bool>> predicate);

    /// <summary>
    /// InsertAsync method.
    /// </summary>
    Task InsertAsync(OrderEntity order);

    /// <summary>
    /// UpdateAsync method.
    /// </summary>
    Task UpdateAsync(OrderEntity order);

    /// <summary>
    /// GetAllAsync method.
    /// </summary>
    Task<IEnumerable<OrderEntity>> GetAllAsync();

    /// <summary>
    /// FindByCustomerAsync method.
    /// </summary>
    Task<Result<OrderEntity?, AeroError>> FindByCustomerAsync(string customerId, CancellationToken ct = default);
}
