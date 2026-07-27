using System.Globalization;
using Aero.Cms.Modules.Commerce.Catalog.Models;
using Aero.Cms.Modules.Commerce.Catalog.Services;
using Aero.Core.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Commerce.Catalog.Api;

/// <summary>Maps anonymous storefront listing reads and selected-site manager mutations.</summary>
public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogApi(this IEndpointRouteBuilder builder)
    {
        var storefront = builder.MapGroup("/api/commerce/catalog").AllowAnonymous();
        storefront.MapGet("/listings", async (string? search, string? category, int? skip, int? take, IProductService service, ISiteContext site, CancellationToken ct) =>
        {
            var validationFailure = ValidateStorefrontQuery(search, category, skip, take);
            if (validationFailure is not null) return validationFailure;
            var result = await service.SearchPublishedAsync(site.TenantId, site.SiteId, CultureInfo.CurrentUICulture.Name, search, category, skip ?? 0, take ?? 20, ct: ct);
            return result is Result<(IReadOnlyList<ProductListingDocument> Items, long TotalCount), AeroError>.Ok(var page)
                ? Results.Ok(new PublicListingPage(page.Items.Select(PublicListingResponse.From).ToList(), page.TotalCount))
                : Failure(((Result<(IReadOnlyList<ProductListingDocument> Items, long TotalCount), AeroError>.Failure)result).Error);
        });
        storefront.MapGet("/listings/by-slug/{slug}", async (string slug, IProductService service, ISiteContext site, CancellationToken ct) =>
        {
            if (!CatalogSlug.IsCanonical(slug)) return Results.NotFound();
            var result = await service.GetPublishedListingBySlugAsync(site.TenantId, site.SiteId, CultureInfo.CurrentUICulture.Name, slug, ct);
            return result switch
            {
                Result<ProductListingDocument?, AeroError>.Ok { Value: { } listing } => Results.Ok(PublicListingResponse.From(listing)),
                Result<ProductListingDocument?, AeroError>.Ok => Results.NotFound(),
                Result<ProductListingDocument?, AeroError>.Failure failure => Failure(failure.Error),
                _ => Results.Problem("Catalog operation failed.", statusCode: StatusCodes.Status500InternalServerError)
            };
        });
        storefront.MapGet("/categories", async (IProductService service, ISiteContext site, CancellationToken ct) =>
        {
            var result = await service.GetPublishedCategoriesAsync(site.TenantId, site.SiteId, CultureInfo.CurrentUICulture.Name, ct);
            return result is Result<IReadOnlyList<string>, AeroError>.Ok(var categories)
                ? Results.Ok(categories)
                : Failure(((Result<IReadOnlyList<string>, AeroError>.Failure)result).Error);
        });

        var manager = builder.MapGroup("/api/v1/admin/commerce/catalog");
        manager.MapGet("/products", GetProducts).RequireAuthorization("site:read");
        manager.MapGet("/products/{id:long}", GetProduct).RequireAuthorization("site:read");
        manager.MapPost("/products", CreateProduct).RequireAuthorization("site:create");
        manager.MapPut("/products/{id:long}", UpdateProduct).RequireAuthorization("site:update");
        manager.MapDelete("/products/{id:long}", DeleteProduct).RequireAuthorization("site:delete");
        manager.MapGet("/listings", GetListings).RequireAuthorization("site:read");
        manager.MapGet("/listings/{id:long}", GetListing).RequireAuthorization("site:read");
        manager.MapPost("/listings", CreateListing).RequireAuthorization("site:create");
        manager.MapPut("/listings/{id:long}", UpdateListing).RequireAuthorization("site:update");
        manager.MapDelete("/listings/{id:long}", DeleteListing).RequireAuthorization("site:delete");
        return builder;
    }

    private static async Task<IResult> GetProducts(
        string? search,
        int? skip,
        int? take,
        IProductService service,
        ICommerceManagerScopeResolver scopeResolver,
        CancellationToken ct)
    {
        var scope = await scopeResolver.ResolveAsync(ct);
        if (scope is Result<CommerceManagerScope, AeroError>.Failure scopeFailure) return Failure(scopeFailure.Error);
        var value = ((Result<CommerceManagerScope, AeroError>.Ok)scope).Value;
        var result = await service.SearchProductsAsync(value.TenantId, search, skip ?? 0, take ?? 20, ct);
        return result is Result<(IReadOnlyList<ProductDocument> Items, long TotalCount), AeroError>.Ok(var page)
            ? Results.Ok(new CatalogManagerPage<ManagerProductResponse>(page.Items.Select(ManagerProductResponse.From).ToList(), page.TotalCount))
            : Failure(((Result<(IReadOnlyList<ProductDocument> Items, long TotalCount), AeroError>.Failure)result).Error);
    }

    private static async Task<IResult> GetProduct(
        long id,
        IProductService service,
        ICommerceManagerScopeResolver scopeResolver,
        CancellationToken ct)
    {
        var scope = await scopeResolver.ResolveAsync(ct);
        if (scope is Result<CommerceManagerScope, AeroError>.Failure scopeFailure) return Failure(scopeFailure.Error);
        var value = ((Result<CommerceManagerScope, AeroError>.Ok)scope).Value;
        var result = await service.GetProductAsync(value.TenantId, id, ct);
        return result switch
        {
            Result<ProductDocument?, AeroError>.Ok { Value: { } product } => Results.Ok(ManagerProductResponse.From(product)),
            Result<ProductDocument?, AeroError>.Ok => Results.NotFound(),
            Result<ProductDocument?, AeroError>.Failure failure => Failure(failure.Error),
            _ => Results.Problem("Catalog operation failed.", statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    private static async Task<IResult> CreateProduct(
        ProductRequest request,
        IProductService service,
        ICommerceManagerScopeResolver scopeResolver,
        CancellationToken ct)
    {
        var scope = await scopeResolver.ResolveAsync(ct);
        if (scope is Result<CommerceManagerScope, AeroError>.Failure scopeFailure) return Failure(scopeFailure.Error);
        var value = ((Result<CommerceManagerScope, AeroError>.Ok)scope).Value;
        var result = await service.CreateProductAsync(value.TenantId, request.ToDocument(), ct);
        return result is Result<ProductDocument, AeroError>.Ok(var product)
            ? Results.Created($"/api/v1/admin/commerce/catalog/products/{product.Id}", ManagerProductResponse.From(product))
            : Failure(((Result<ProductDocument, AeroError>.Failure)result).Error);
    }

    private static async Task<IResult> UpdateProduct(
        long id,
        ProductRequest request,
        IProductService service,
        ICommerceManagerScopeResolver scopeResolver,
        CancellationToken ct)
    {
        var scope = await scopeResolver.ResolveAsync(ct);
        if (scope is Result<CommerceManagerScope, AeroError>.Failure scopeFailure) return Failure(scopeFailure.Error);
        var value = ((Result<CommerceManagerScope, AeroError>.Ok)scope).Value;
        var result = await service.UpdateProductAsync(value.TenantId, id, request.ToDocument(), ct);
        return result is Result<ProductDocument, AeroError>.Ok(var product)
            ? Results.Ok(ManagerProductResponse.From(product))
            : Failure(((Result<ProductDocument, AeroError>.Failure)result).Error);
    }

    private static async Task<IResult> DeleteProduct(
        long id,
        IProductService service,
        ICommerceManagerScopeResolver scopeResolver,
        CancellationToken ct)
    {
        var scope = await scopeResolver.ResolveAsync(ct);
        if (scope is Result<CommerceManagerScope, AeroError>.Failure scopeFailure) return Failure(scopeFailure.Error);
        var value = ((Result<CommerceManagerScope, AeroError>.Ok)scope).Value;
        var result = await service.DeleteProductAsync(value.TenantId, id, ct);
        return result switch
        {
            Result<bool, AeroError>.Ok { Value: true } => Results.NoContent(),
            Result<bool, AeroError>.Ok => Results.NotFound(),
            Result<bool, AeroError>.Failure failure => Failure(failure.Error),
            _ => Results.Problem("Catalog operation failed.", statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    private static async Task<IResult> GetListings(
        string? culture,
        string? search,
        int? skip,
        int? take,
        IProductService service,
        ICommerceManagerScopeResolver scopeResolver,
        CancellationToken ct)
    {
        var scope = await scopeResolver.ResolveAsync(ct);
        if (scope is Result<CommerceManagerScope, AeroError>.Failure scopeFailure) return Failure(scopeFailure.Error);
        var value = ((Result<CommerceManagerScope, AeroError>.Ok)scope).Value;
        var result = await service.SearchListingsAsync(value.TenantId, value.SiteId, culture, search, skip ?? 0, take ?? 20, ct);
        return result is Result<(IReadOnlyList<ProductListingDocument> Items, long TotalCount), AeroError>.Ok(var page)
            ? Results.Ok(new CatalogManagerPage<ManagerListingResponse>(page.Items.Select(ManagerListingResponse.From).ToList(), page.TotalCount))
            : Failure(((Result<(IReadOnlyList<ProductListingDocument> Items, long TotalCount), AeroError>.Failure)result).Error);
    }

    private static async Task<IResult> GetListing(
        long id,
        IProductService service,
        ICommerceManagerScopeResolver scopeResolver,
        CancellationToken ct)
    {
        var scope = await scopeResolver.ResolveAsync(ct);
        if (scope is Result<CommerceManagerScope, AeroError>.Failure scopeFailure) return Failure(scopeFailure.Error);
        var value = ((Result<CommerceManagerScope, AeroError>.Ok)scope).Value;
        var result = await service.GetListingAsync(value.TenantId, value.SiteId, id, ct);
        return result switch
        {
            Result<ProductListingDocument?, AeroError>.Ok { Value: { } listing } => Results.Ok(ManagerListingResponse.From(listing)),
            Result<ProductListingDocument?, AeroError>.Ok => Results.NotFound(),
            Result<ProductListingDocument?, AeroError>.Failure failure => Failure(failure.Error),
            _ => Results.Problem("Catalog operation failed.", statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    private static async Task<IResult> CreateListing(
        ListingRequest request,
        IProductService service,
        ICommerceManagerScopeResolver scopeResolver,
        CancellationToken ct)
    {
        var scope = await scopeResolver.ResolveAsync(ct);
        if (scope is Result<CommerceManagerScope, AeroError>.Failure scopeFailure) return Failure(scopeFailure.Error);
        var value = ((Result<CommerceManagerScope, AeroError>.Ok)scope).Value;
        var result = await service.CreateListingAsync(value.TenantId, value.SiteId, request.ToDocument(), ct);
        return result is Result<ProductListingDocument, AeroError>.Ok(var listing)
            ? Results.Created($"/api/v1/admin/commerce/catalog/listings/{listing.Id}", ManagerListingResponse.From(listing))
            : Failure(((Result<ProductListingDocument, AeroError>.Failure)result).Error);
    }

    private static async Task<IResult> UpdateListing(
        long id,
        ListingRequest request,
        IProductService service,
        ICommerceManagerScopeResolver scopeResolver,
        CancellationToken ct)
    {
        var scope = await scopeResolver.ResolveAsync(ct);
        if (scope is Result<CommerceManagerScope, AeroError>.Failure scopeFailure) return Failure(scopeFailure.Error);
        var value = ((Result<CommerceManagerScope, AeroError>.Ok)scope).Value;
        var result = await service.UpdateListingAsync(value.TenantId, value.SiteId, id, request.ToDocument(), ct);
        return result is Result<ProductListingDocument, AeroError>.Ok(var listing)
            ? Results.Ok(ManagerListingResponse.From(listing))
            : Failure(((Result<ProductListingDocument, AeroError>.Failure)result).Error);
    }

    private static async Task<IResult> DeleteListing(
        long id,
        IProductService service,
        ICommerceManagerScopeResolver scopeResolver,
        CancellationToken ct)
    {
        var scope = await scopeResolver.ResolveAsync(ct);
        if (scope is Result<CommerceManagerScope, AeroError>.Failure scopeFailure) return Failure(scopeFailure.Error);
        var value = ((Result<CommerceManagerScope, AeroError>.Ok)scope).Value;
        var result = await service.DeleteListingAsync(value.TenantId, value.SiteId, id, ct);
        return result switch
        {
            Result<bool, AeroError>.Ok { Value: true } => Results.NoContent(),
            Result<bool, AeroError>.Ok => Results.NotFound(),
            Result<bool, AeroError>.Failure failure => Failure(failure.Error),
            _ => Results.Problem("Catalog operation failed.", statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    private static IResult Failure(AeroError error) => error switch
    {
        AeroError.NotFound => Results.NotFound(),
        AeroError.Conflict conflict => Results.Conflict(new CatalogErrorResponse(conflict.msg)),
        AeroError.Exists exists => Results.Conflict(new CatalogErrorResponse(exists.msg)),
        AeroError.Validation validation => Results.BadRequest(new CatalogValidationErrorResponse(validation.Errors)),
        AeroError.BadRequest badRequest => Results.BadRequest(new CatalogErrorResponse(badRequest.msg)),
        AeroError.InvalidRequest invalidRequest => Results.BadRequest(new CatalogErrorResponse(invalidRequest.msg)),
        _ => Results.Problem("Catalog operation failed.", statusCode: StatusCodes.Status500InternalServerError)
    };

    private static IResult? ValidateStorefrontQuery(string? search, string? category, int? skip, int? take)
    {
        var errors = new List<string>();
        if (search?.Trim().Length > 200) errors.Add("Search must be 200 characters or fewer.");
        if (category?.Trim().Length > 256) errors.Add("Category must be 256 characters or fewer.");
        if (skip is < 0) errors.Add("Skip must be zero or greater.");
        if (take is < 1 or > 100) errors.Add("Take must be between 1 and 100.");
        return errors.Count == 0 ? null : Results.BadRequest(new CatalogValidationErrorResponse(errors));
    }
}

/// <summary>Manager-owned canonical product input. Tenant and product identity are server-derived.</summary>
public sealed record ProductRequest(string Name, string? Description, string Sku, int StockQuantity, bool IsActive, Dictionary<string, string>? Attributes, List<string>? Tags, long Version = 0)
{ public ProductDocument ToDocument() => new() { Name = Name, Description = Description, Sku = Sku, StockQuantity = StockQuantity, IsActive = IsActive, Attributes = Attributes ?? [], Tags = Tags ?? [], Version = Version }; }

/// <summary>Manager-owned listing input. Site and tenant ownership are server-derived.</summary>
public sealed record ListingRequest(long ProductId, string Culture, string Slug, string Name, string? ShortDescription, string? Description, string? Category, string? ImageUrl, decimal Price, decimal? CompareAtPrice, bool IsPublished, bool IsFeatured, long Version = 0, bool IncludeInSearch = true, bool IncludeInPublicAi = false)
{ public ProductListingDocument ToDocument() => new() { ProductId = ProductId, Culture = Culture, Slug = Slug, Name = Name, ShortDescription = ShortDescription, Description = Description, Category = Category, ImageUrl = ImageUrl, Price = Price, CompareAtPrice = CompareAtPrice, IsPublished = IsPublished, IsFeatured = IsFeatured, IncludeInSearch = IncludeInSearch, IncludeInPublicAi = IncludeInPublicAi, Version = Version }; }

public sealed record CatalogManagerPage<T>(IReadOnlyList<T> Items, long TotalCount);
public sealed record ManagerProductResponse(long Id, string Name, string? Description, string Sku, int StockQuantity, bool IsActive, IReadOnlyDictionary<string, string> Attributes, IReadOnlyList<string> Tags, long Version)
{ public static ManagerProductResponse From(ProductDocument product) => new(product.Id, product.Name, product.Description, product.Sku, product.StockQuantity, product.IsActive, product.Attributes, product.Tags, product.Version); }
public sealed record ManagerListingResponse(long Id, long ProductId, string Culture, string Slug, string Name, string? ShortDescription, string? Description, string? Category, string? ImageUrl, decimal Price, decimal? CompareAtPrice, string Currency, bool IsPublished, bool IsFeatured, long Version, bool IncludeInSearch, bool IncludeInPublicAi)
{ public static ManagerListingResponse From(ProductListingDocument listing) => new(listing.Id, listing.ProductId, listing.Culture, listing.Slug, listing.Name, listing.ShortDescription, listing.Description, listing.Category, listing.ImageUrl, listing.Price, listing.CompareAtPrice, listing.Currency, listing.IsPublished, listing.IsFeatured, listing.Version, listing.IncludeInSearch, listing.IncludeInPublicAi); }
public sealed record CatalogErrorResponse(string Error);
public sealed record CatalogValidationErrorResponse(IReadOnlyList<string> Errors);
