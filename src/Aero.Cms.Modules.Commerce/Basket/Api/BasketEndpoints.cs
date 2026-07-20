using Aero.Cms.Modules.Commerce.Basket.Models;
using Aero.Cms.Modules.Commerce.Basket.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Commerce.Basket.Api;

/// <summary>
/// Registers authenticated HTTP endpoints for reading and mutating baskets.
/// </summary>
/// <remarks>
/// Every route in this group requires authorization. The <c>customerId</c> value is nevertheless supplied by model
/// binding rather than derived from the authenticated principal, so these endpoints do not themselves establish that
/// the caller owns the addressed basket. They also expose no tenant or site filter.
/// </remarks>
public static class BasketEndpoints
{
    /// <summary>
    /// Maps the <c>/api/commerce/basket</c> route group.
    /// </summary>
    /// <param name="builder">The route builder to which the authenticated routes are added.</param>
    /// <returns>The supplied <paramref name="builder"/>.</returns>
    /// <remarks>
    /// GET <c>/</c> returns or creates a basket; POST <c>/items</c> adds or increments an item; DELETE
    /// <c>/items/{productId}</c> removes matching items; and DELETE <c>/</c> clears items. Each route returns the
    /// resulting basket with 200 on a service success and 400 for a service failure. No endpoint accepts an
    /// idempotency key or performs stock, pricing, or ownership validation.
    /// </remarks>
    public static IEndpointRouteBuilder MapBasketApi(this IEndpointRouteBuilder builder)
    {
        var group = builder
            .MapGroup("/api/commerce/basket")
            .RequireAuthorization();

        // Get current basket for the authenticated user
        group.MapGet("/", async (
            string customerId,
            IBasketService? service = null) =>
        {
            var result = await service!.GetOrCreateBasketAsync(customerId);
            if (result is Result<BasketDocument, AeroError>.Ok(var basket))
                return Results.Ok(basket);
            return Results.BadRequest();
        });

        // Add item to basket
        group.MapPost("/items", async (
            string customerId,
            BasketItem item,
            IBasketService? service = null) =>
        {
            var result = await service!.AddItemAsync(customerId, item);
            if (result is Result<BasketDocument, AeroError>.Ok(var basket))
                return Results.Ok(basket);
            return Results.BadRequest();
        });

        // Remove item from basket
        group.MapDelete("/items/{productId:long}", async (
            string customerId,
            long productId,
            IBasketService? service = null) =>
        {
            var result = await service!.RemoveItemAsync(customerId, productId);
            if (result is Result<BasketDocument, AeroError>.Ok(var basket))
                return Results.Ok(basket);
            return Results.BadRequest();
        });

        // Clear basket
        group.MapDelete("/", async (
            string customerId,
            IBasketService? service = null) =>
        {
            var result = await service!.ClearBasketAsync(customerId);
            if (result is Result<BasketDocument, AeroError>.Ok(var basket))
                return Results.Ok(basket);
            return Results.BadRequest();
        });

        return builder;
    }
}
