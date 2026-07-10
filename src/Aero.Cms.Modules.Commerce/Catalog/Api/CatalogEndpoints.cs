using Aero.Cms.Modules.Commerce.Catalog.Models;
using Aero.Cms.Modules.Commerce.Catalog.Services;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Commerce.Catalog.Api;

public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogApi(this IEndpointRouteBuilder builder)
    {
        var group = builder
            .MapGroup("/api/commerce/catalog")
            .RequireAuthorization();

        // List / search products
        group.MapGet("/products", async (
            string? search,
            string? category,
            decimal? minPrice,
            decimal? maxPrice,
            int skip = 0,
            int take = 20,
            IProductService? service = null) =>
        {
            var result = await service!.SearchAsync(search, category, minPrice, maxPrice, skip, take);
            if (result is Result<(IReadOnlyList<ProductDocument> Items, long TotalCount), AeroError>.Ok(var ok))
                return Results.Ok(new { ok.Items, ok.TotalCount });
            return Results.BadRequest();
        });

        // Get by ID
        group.MapGet("/products/{id:long}", async (
            long id,
            IProductService? service = null) =>
        {
            var result = await service!.GetByIdAsync(id);
            if (result is Result<ProductDocument?, AeroError>.Ok(var product) && product is not null)
                return Results.Ok(product);
            return Results.NotFound();
        });

        // Get by slug
        group.MapGet("/products/by-slug/{slug}", async (
            string slug,
            IProductService? service = null) =>
        {
            var result = await service!.FindBySlugAsync(slug);
            if (result is Result<ProductDocument?, AeroError>.Ok(var product) && product is not null)
                return Results.Ok(product);
            return Results.NotFound();
        });

        // Create
        group.MapPost("/products", async (
            ProductDocument product,
            IProductService? service = null,
            IValidator<ProductDocument>? validator = null) =>
        {
            var validation = await validator!.ValidateAsync(product);
            if (!validation.IsValid)
                return Results.ValidationProblem(validation.ToDictionary());

            await service!.InsertAsync(product);
            return Results.Created($"/api/commerce/catalog/products/{product.Id}", product);
        });

        // Update
        group.MapPut("/products/{id:long}", async (
            long id,
            ProductDocument product,
            IProductService? service = null,
            IValidator<ProductDocument>? validator = null) =>
        {
            product.Id = id;
            var validation = await validator!.ValidateAsync(product);
            if (!validation.IsValid)
                return Results.ValidationProblem(validation.ToDictionary());

            await service!.UpdateAsync(product);
            return Results.Ok(product);
        });

        // Delete
        group.MapDelete("/products/{id:long}", async (
            long id,
            IProductService? service = null) =>
        {
            await service!.DeleteAsync(id);
            return Results.NoContent();
        });

        return builder;
    }
}
