using Aero.Cms.Modules.Commerce.Basket.Models;

namespace Aero.Cms.Modules.Commerce.Basket.Services;

public interface IBasketService : IGenericMartenRepository<BasketDocument>
{
    /// <summary>
    /// Gets the basket for a customer, creating one if it doesn't exist.
    /// </summary>
    Task<Result<BasketDocument, AeroError>> GetOrCreateBasketAsync(string customerId, CancellationToken ct = default);

    /// <summary>
    /// Adds an item to the customer's basket. Increments quantity if the product already exists.
    /// </summary>
    Task<Result<BasketDocument, AeroError>> AddItemAsync(string customerId, BasketItem item, CancellationToken ct = default);

    /// <summary>
    /// Removes an item from the customer's basket by product ID.
    /// </summary>
    Task<Result<BasketDocument, AeroError>> RemoveItemAsync(string customerId, long productId, CancellationToken ct = default);

    /// <summary>
    /// Clears all items from the customer's basket.
    /// </summary>
    Task<Result<BasketDocument, AeroError>> ClearBasketAsync(string customerId, CancellationToken ct = default);
}
