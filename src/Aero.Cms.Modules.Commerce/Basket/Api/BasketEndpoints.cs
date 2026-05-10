using Aero.Cms.Modules.Commerce.Basket.Models;
using Aero.Cms.Modules.Commerce.Basket.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Commerce.Basket.Api;

public static class BasketEndpoints
{
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
