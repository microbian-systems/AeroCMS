using System.Linq.Expressions;
using Aero.Cms.Modules.Commerce.Basket.Models;
using AeroDB.Sable;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Commerce.Basket.Services;

public sealed class BasketService(IDocumentSession docSession, ILogger<BasketService> log)
    : IBasketService
{
    public async Task<Result<BasketDocument?, AeroError>> GetByIdAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var basket = await docSession.LoadAsync<BasketDocument>(id, ct);
            return basket is null
                ? Prelude.Ok<BasketDocument?, AeroError>(null)
                : Prelude.Ok<BasketDocument?, AeroError>(basket);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<BasketDocument?, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    public async Task<Result<IReadOnlyList<BasketDocument>, AeroError>> GetAllAsync(CancellationToken ct = default)
    {
        try
        {
            var items = await docSession.Query<BasketDocument>().ToListAsync(ct);
            return Prelude.Ok<IReadOnlyList<BasketDocument>, AeroError>(items);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<IReadOnlyList<BasketDocument>, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    public async Task<Result<IReadOnlyList<BasketDocument>, AeroError>> FindAsync(
        Expression<Func<BasketDocument, bool>> predicate, CancellationToken ct = default)
    {
        try
        {
            var items = await docSession.Query<BasketDocument>().Where(predicate).ToListAsync(ct);
            return Prelude.Ok<IReadOnlyList<BasketDocument>, AeroError>(items);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<IReadOnlyList<BasketDocument>, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    public async Task<Result<BasketDocument, AeroError>> InsertAsync(BasketDocument entity, CancellationToken ct = default)
    {
        try
        {
            docSession.Store(entity);
            await docSession.SaveChangesAsync(ct);
            return Prelude.Ok<BasketDocument, AeroError>(entity);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<BasketDocument, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    public async Task<Result<BasketDocument, AeroError>> UpdateAsync(BasketDocument entity, CancellationToken ct = default)
    {
        try
        {
            docSession.Store(entity);
            await docSession.SaveChangesAsync(ct);
            return Prelude.Ok<BasketDocument, AeroError>(entity);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<BasketDocument, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    public async Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default)
    {
        try
        {
            docSession.Delete<BasketDocument>(id);
            await docSession.SaveChangesAsync(ct);
            return Prelude.Ok<bool, AeroError>(true);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<bool, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    public async Task<Result<long, AeroError>> CountAsync(CancellationToken ct = default)
    {
        try
        {
            var count = await docSession.Query<BasketDocument>().CountAsync(ct);
            return Prelude.Ok<long, AeroError>(count);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<long, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    public async Task<Result<BasketDocument, AeroError>> GetOrCreateBasketAsync(string customerId, CancellationToken ct = default)
    {
        try
        {
            var basket = await docSession.Query<BasketDocument>()
                .FirstOrDefaultAsync(b => b.CustomerId == customerId, ct);

            if (basket is not null)
                return Prelude.Ok<BasketDocument, AeroError>(basket);

            basket = new BasketDocument
            {
                Id = Snowflake.NewId(),
                CustomerId = customerId,
                Items = [],
                CreatedOn = DateTimeOffset.UtcNow
            };

            docSession.Store(basket);
            await docSession.SaveChangesAsync(ct);
            return Prelude.Ok<BasketDocument, AeroError>(basket);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<BasketDocument, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    public async Task<Result<BasketDocument, AeroError>> AddItemAsync(string customerId, BasketItem item, CancellationToken ct = default)
    {
        try
        {
            var basketResult = await GetOrCreateBasketAsync(customerId, ct);
            if (basketResult is Result<BasketDocument, AeroError>.Failure fail)
                return fail;

            var basket = ((Result<BasketDocument, AeroError>.Ok)basketResult).Value;
            var existing = basket.Items.FirstOrDefault(i => i.ProductId == item.ProductId);

            if (existing is not null)
            {
                // Increment quantity
                var idx = basket.Items.IndexOf(existing);
                basket.Items[idx] = existing with { Quantity = existing.Quantity + item.Quantity };
            }
            else
            {
                basket.Items.Add(item);
            }

            basket.ModifiedOn = DateTimeOffset.UtcNow;
            docSession.Store(basket);
            await docSession.SaveChangesAsync(ct);
            return Prelude.Ok<BasketDocument, AeroError>(basket);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<BasketDocument, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    public async Task<Result<BasketDocument, AeroError>> RemoveItemAsync(string customerId, long productId, CancellationToken ct = default)
    {
        try
        {
            var basketResult = await GetOrCreateBasketAsync(customerId, ct);
            if (basketResult is Result<BasketDocument, AeroError>.Failure fail)
                return fail;

            var basket = ((Result<BasketDocument, AeroError>.Ok)basketResult).Value;
            basket.Items.RemoveAll(i => i.ProductId == productId);
            basket.ModifiedOn = DateTimeOffset.UtcNow;

            docSession.Store(basket);
            await docSession.SaveChangesAsync(ct);
            return Prelude.Ok<BasketDocument, AeroError>(basket);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<BasketDocument, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    public async Task<Result<BasketDocument, AeroError>> ClearBasketAsync(string customerId, CancellationToken ct = default)
    {
        try
        {
            var basketResult = await GetOrCreateBasketAsync(customerId, ct);
            if (basketResult is Result<BasketDocument, AeroError>.Failure fail)
                return fail;

            var basket = ((Result<BasketDocument, AeroError>.Ok)basketResult).Value;
            basket.Items.Clear();
            basket.ModifiedOn = DateTimeOffset.UtcNow;

            docSession.Store(basket);
            await docSession.SaveChangesAsync(ct);
            return Prelude.Ok<BasketDocument, AeroError>(basket);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<BasketDocument, AeroError>(AeroError.CreateError(ex.Message));
        }
    }
}
