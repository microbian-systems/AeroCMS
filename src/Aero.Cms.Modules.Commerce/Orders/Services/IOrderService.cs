using System.Linq.Expressions;
using Aero.Cms.Modules.Commerce.Orders.Domain;
using Aero.Core;

namespace Aero.Cms.Modules.Commerce.Orders.Services;

/// <summary>
/// Provides order document retrieval and persistence operations.
/// </summary>
/// <remarks>
/// This interface does not establish caller ownership, tenant or site scope, transaction boundaries with basket or
/// event operations, idempotency, or optimistic concurrency. Its basic persistence methods surface store exceptions
/// to the caller; only <see cref="FindByCustomerAsync"/> maps failures into <see cref="Result{T,TError}"/>.
/// </remarks>
public interface IOrderService
{
    /// <summary>
    /// Loads an order by document identifier, returning <see langword="null"/> when it is absent.
    /// </summary>
    Task<OrderEntity?> FindByIdAsync(long id);

    /// <summary>
    /// Queries orders with a provider-translatable predicate.
    /// </summary>
    Task<IEnumerable<OrderEntity>> FindAsync(Expression<Func<OrderEntity, bool>> predicate);

    /// <summary>
    /// Stores an order and commits the current document session.
    /// </summary>
    Task InsertAsync(OrderEntity order);

    /// <summary>
    /// Stores an order and commits the current document session.
    /// </summary>
    Task UpdateAsync(OrderEntity order);

    /// <summary>
    /// Enumerates every order visible to the current document session.
    /// </summary>
    Task<IEnumerable<OrderEntity>> GetAllAsync();

    /// <summary>
    /// Finds the first order with the supplied customer identifier.
    /// </summary>
    /// <remarks>A missing order and an operational exception are both returned as a failed result.</remarks>
    Task<Result<OrderEntity?, AeroError>> FindByCustomerAsync(string customerId, CancellationToken ct = default);
}
