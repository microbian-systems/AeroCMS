using System.Linq.Expressions;
using Aero.Cms.Modules.Commerce.Orders.Domain;
using AeroDB.Sable;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Commerce.Orders.Services;

/// <summary>
/// Order persistence via AeroDB.Sable document store (ported from EF Core Npgsql).
/// </summary>
public sealed class OrderService : IOrderService
{
    private readonly IDocumentSession _session;
    private readonly ILogger<OrderService> _log;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderService"/> class.
    /// </summary>
    public OrderService(IDocumentSession session, ILogger<OrderService> log)
    {
        _session = session;
        _log = log;
    }

    /// <inheritdoc />
    public async Task<OrderEntity?> FindByIdAsync(long id)
    {
        return await _session.LoadAsync<OrderEntity>(id);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<OrderEntity>> FindAsync(Expression<Func<OrderEntity, bool>> predicate)
    {
        return await _session.Query<OrderEntity>().Where(predicate).ToListAsync();
    }

    /// <inheritdoc />
    public Task InsertAsync(OrderEntity order)
    {
        _session.Store(order);
        return _session.SaveChangesAsync();
    }

    /// <inheritdoc />
    public Task UpdateAsync(OrderEntity order)
    {
        _session.Store(order);
        return _session.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<OrderEntity>> GetAllAsync()
    {
        return await _session.Query<OrderEntity>().ToListAsync();
    }

    /// <inheritdoc />
    public async Task<Result<OrderEntity?, AeroError>> FindByCustomerAsync(string customerId, CancellationToken ct = default)
    {
        try
        {
            var order = await _session.Query<OrderEntity>()
                .FirstOrDefaultAsync(o => o.CustomerId == customerId, ct);

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
