using Aero.Cms.Modules.Commerce.Basket.Models;
using Marten;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Commerce.Basket.Services;

public sealed class BasketService(IDocumentSession session, ILogger<BasketService> log)
    : GenericMartenRepository<BasketDocument>(session, log), IBasketService
{
    public async Task<Result<BasketDocument, AeroError>> GetOrCreateBasketAsync(string customerId, CancellationToken ct = default)
    {
        try
        {
            var basket = await session.Query<BasketDocument>()
                .FirstOrDefaultAsync(b => b.CustomerId == customerId, token: ct);

            if (basket is not null)
                return Prelude.Ok<BasketDocument, AeroError>(basket);

            basket = new BasketDocument
            {
                Id = Snowflake.NewId(),
                CustomerId = customerId,
                Items = [],
                CreatedOn = DateTimeOffset.UtcNow
            };

            session.Store(basket);
            await session.SaveChangesAsync(ct);
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
            session.Store(basket);
            await session.SaveChangesAsync(ct);
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

            session.Store(basket);
            await session.SaveChangesAsync(ct);
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

            session.Store(basket);
            await session.SaveChangesAsync(ct);
            return Prelude.Ok<BasketDocument, AeroError>(basket);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<BasketDocument, AeroError>(AeroError.CreateError(ex.Message));
        }
    }
}
