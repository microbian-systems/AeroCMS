using System.Globalization;
using System.Text.Json.Serialization;
using Aero.Cms.Modules.Commerce.Catalog.Models;
using AeroDB.Sable;
using FluentValidation;

namespace Aero.Cms.Modules.Commerce.Catalog.Services;

/// <summary>Scoped catalog persistence. Listings are never returned unless their canonical product is active in the same tenant.</summary>
public sealed class ProductService(IDocumentSession session, IValidator<ProductDocument> productValidator, IValidator<ProductListingDocument> listingValidator) : IProductService
{
    public async Task<Result<ProductDocument?, AeroError>> GetProductAsync(long tenantId, long productId, CancellationToken ct = default)
        => await Execute(async () => await session.Query<ProductDocument>().FirstOrDefaultAsync(x => x.Id == productId && x.TenantId == tenantId, ct));

    public async Task<Result<(IReadOnlyList<ProductDocument> Items, long TotalCount), AeroError>> SearchProductsAsync(
        long tenantId,
        string? search = null,
        int skip = 0,
        int take = 20,
        CancellationToken ct = default)
    {
        try
        {
            var query = session.Query<ProductDocument>().Where(x => x.TenantId == tenantId);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLowerInvariant();
                query = query.Where(x =>
                    x.Name.ToLower().Contains(term) ||
                    x.Sku.ToLower().Contains(term) ||
                    (x.Description != null && x.Description.ToLower().Contains(term)));
            }

            var scopedQuery = (ISurrealDbQueryable<ProductDocument>)query;
            var totalCount = await scopedQuery.CountAsync(ct);
            var items = await scopedQuery
                .OrderBy(x => x.Name)
                .Skip(Math.Max(0, skip))
                .Take(Math.Clamp(take, 1, 100))
                .ToListAsync(ct);
            return Prelude.Ok<(IReadOnlyList<ProductDocument>, long), AeroError>(
                (items, totalCount));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return Prelude.Fail<(IReadOnlyList<ProductDocument>, long), AeroError>(
                AeroError.DatabaseError("Products could not be loaded."));
        }
    }

    public async Task<Result<ProductListingDocument?, AeroError>> GetListingAsync(
        long tenantId,
        long siteId,
        long listingId,
        CancellationToken ct = default)
        => await Execute(async () => await session.Query<ProductListingDocument>().FirstOrDefaultAsync(
            x => x.Id == listingId && x.TenantId == tenantId && x.SiteId == siteId,
            ct));

    public async Task<Result<(IReadOnlyList<ProductListingDocument> Items, long TotalCount), AeroError>> SearchListingsAsync(
        long tenantId,
        long siteId,
        string? culture = null,
        string? search = null,
        int skip = 0,
        int take = 20,
        CancellationToken ct = default)
    {
        try
        {
            var query = session.Query<ProductListingDocument>()
                .Where(x => x.TenantId == tenantId && x.SiteId == siteId);

            if (!string.IsNullOrWhiteSpace(culture))
            {
                if (!TryNormalizeCulture(culture, out var canonicalCulture))
                    return Prelude.Fail<(IReadOnlyList<ProductListingDocument>, long), AeroError>(
                        AeroError.ValidationError(["Culture is invalid."]));
                query = query.Where(x => x.Culture == canonicalCulture);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLowerInvariant();
                query = query.Where(x =>
                    x.Name.ToLower().Contains(term) ||
                    x.Slug.ToLower().Contains(term) ||
                    (x.Description != null && x.Description.ToLower().Contains(term)));
            }

            var scopedQuery = (ISurrealDbQueryable<ProductListingDocument>)query;
            var totalCount = await scopedQuery.CountAsync(ct);
            var items = await scopedQuery
                .OrderBy(x => x.Name)
                .Skip(Math.Max(0, skip))
                .Take(Math.Clamp(take, 1, 100))
                .ToListAsync(ct);
            return Prelude.Ok<(IReadOnlyList<ProductListingDocument>, long), AeroError>(
                (items, totalCount));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return Prelude.Fail<(IReadOnlyList<ProductListingDocument>, long), AeroError>(
                AeroError.DatabaseError("Listings could not be loaded."));
        }
    }

    public async Task<Result<ProductListingDocument?, AeroError>> GetPublishedListingBySlugAsync(long tenantId, long siteId, string culture, string slug, CancellationToken ct = default)
    {
        var listingResult = await Execute(() => session.Query<ProductListingDocument>().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.SiteId == siteId && x.Culture == culture && x.Slug == slug && x.IsPublished && x.Currency == "USD", ct));
        if (listingResult is not Result<ProductListingDocument?, AeroError>.Ok(var listing) || listing is null) return listingResult;
        var product = await GetProductAsync(tenantId, listing.ProductId, ct);
        return product switch
        {
            Result<ProductDocument?, AeroError>.Ok { Value: { IsActive: true } } => Prelude.Ok<ProductListingDocument?, AeroError>(listing),
            Result<ProductDocument?, AeroError>.Ok => Prelude.Ok<ProductListingDocument?, AeroError>(null),
            Result<ProductDocument?, AeroError>.Failure failure => Prelude.Fail<ProductListingDocument?, AeroError>(failure.Error),
            _ => Prelude.Fail<ProductListingDocument?, AeroError>(AeroError.DatabaseError("Catalog data could not be loaded."))
        };
    }

    public async Task<Result<ProductListingDocument?, AeroError>> GetPublishedListingAsync(long tenantId, long siteId, string culture, long listingId, CancellationToken ct = default)
    {
        var listing = await Execute(() => session.Query<ProductListingDocument>().FirstOrDefaultAsync(x => x.Id == listingId && x.TenantId == tenantId && x.SiteId == siteId && x.Culture == culture && x.IsPublished && x.Currency == "USD", ct));
        if (listing is not Result<ProductListingDocument?, AeroError>.Ok(var value) || value is null) return listing;
        var product = await GetProductAsync(tenantId, value.ProductId, ct);
        return product switch
        {
            Result<ProductDocument?, AeroError>.Ok { Value: { IsActive: true } } => listing,
            Result<ProductDocument?, AeroError>.Ok => Prelude.Ok<ProductListingDocument?, AeroError>(null),
            Result<ProductDocument?, AeroError>.Failure failure => Prelude.Fail<ProductListingDocument?, AeroError>(failure.Error),
            _ => Prelude.Fail<ProductListingDocument?, AeroError>(AeroError.DatabaseError("Catalog data could not be loaded."))
        };
    }

    public async Task<Result<(IReadOnlyList<ProductListingDocument> Items, long TotalCount), AeroError>> SearchPublishedAsync(long tenantId, long siteId, string culture, string? search = null, string? category = null, int skip = 0, int take = 20, bool featuredOnly = false, CancellationToken ct = default)
    {
        try
        {
            var parameters = new Dictionary<string, object?>
            {
                ["tenant_id"] = tenantId,
                ["site_id"] = siteId,
                ["culture"] = culture,
                ["skip"] = Math.Max(0, skip),
                ["take"] = Math.Clamp(take, 1, 100)
            };
            var predicates = new List<string>
            {
                "tenant_id = $tenant_id",
                "site_id = $site_id",
                "culture = $culture",
                "is_published = true",
                "currency = 'USD'",
                "product_id IN (SELECT VALUE type::int(record::id(id)) FROM product_document WHERE tenant_id = $tenant_id AND is_active = true)"
            };
            if (!string.IsNullOrWhiteSpace(search))
            {
                parameters["search"] = search.Trim().ToLowerInvariant();
                predicates.Add("(string::lowercase(name) CONTAINS $search OR string::lowercase(short_description ?? '') CONTAINS $search OR string::lowercase(description ?? '') CONTAINS $search)");
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                parameters["category"] = category.Trim().ToLowerInvariant();
                predicates.Add("string::lowercase(category ?? '') = $category");
            }

            if (featuredOnly)
                predicates.Add("is_featured = true");

            var where = string.Join(" AND ", predicates);
            var counts = await session.RawQueryAsync<CatalogCountRow>(
                $"SELECT count() AS total_count FROM product_listing_document WHERE {where} GROUP ALL;",
                parameters,
                ct);
            var totalCount = counts.FirstOrDefault()?.TotalCount ?? 0;
            var items = await session.RawQueryAsync<ProductListingDocument>(
                $"SELECT * FROM product_listing_document WHERE {where} ORDER BY name ASC, id ASC LIMIT $take START $skip;",
                parameters,
                ct);
            return Prelude.Ok<(IReadOnlyList<ProductListingDocument>, long), AeroError>((items, totalCount));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception) { return Prelude.Fail<(IReadOnlyList<ProductListingDocument>, long), AeroError>(AeroError.DatabaseError("Published listings could not be loaded.")); }
    }

    public async Task<Result<IReadOnlyList<string>, AeroError>> GetPublishedCategoriesAsync(long tenantId, long siteId, string culture, CancellationToken ct = default)
    {
        try
        {
            var parameters = new Dictionary<string, object?>
            {
                ["tenant_id"] = tenantId,
                ["site_id"] = siteId,
                ["culture"] = culture
            };
            var listings = await session.RawQueryAsync<ProductListingDocument>(
                "SELECT * FROM product_listing_document WHERE tenant_id = $tenant_id AND site_id = $site_id " +
                "AND culture = $culture AND is_published = true AND currency = 'USD' " +
                "AND category != NONE AND string::trim(category) != '' " +
                "AND product_id IN (SELECT VALUE type::int(record::id(id)) FROM product_document WHERE tenant_id = $tenant_id AND is_active = true);",
                parameters,
                ct);
            var categories = listings
                .Where(x => !string.IsNullOrWhiteSpace(x.Category))
                .Select(x => x.Category!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return Prelude.Ok<IReadOnlyList<string>, AeroError>(categories);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception) { return Prelude.Fail<IReadOnlyList<string>, AeroError>(AeroError.DatabaseError("Catalog categories could not be loaded.")); }
    }

    public async Task<Result<IReadOnlyList<ProductListingDocument>, AeroError>> GetRecentPublishedAsync(
        long tenantId,
        long siteId,
        string culture,
        int take = 6,
        CancellationToken ct = default)
    {
        try
        {
            var parameters = new Dictionary<string, object?>
            {
                ["tenant_id"] = tenantId,
                ["site_id"] = siteId,
                ["culture"] = culture,
                ["take"] = Math.Clamp(take, 1, 24)
            };
            var listings = await session.RawQueryAsync<ProductListingDocument>(
                "SELECT * FROM product_listing_document WHERE tenant_id = $tenant_id AND site_id = $site_id " +
                "AND culture = $culture AND is_published = true AND currency = 'USD' " +
                "AND product_id IN (SELECT VALUE type::int(record::id(id)) FROM product_document WHERE tenant_id = $tenant_id AND is_active = true) " +
                "ORDER BY created_on DESC, id DESC LIMIT $take;",
                parameters,
                ct);
            return Prelude.Ok<IReadOnlyList<ProductListingDocument>, AeroError>(listings);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception) { return Prelude.Fail<IReadOnlyList<ProductListingDocument>, AeroError>(AeroError.DatabaseError("Recent listings could not be loaded.")); }
    }

    public async Task<Result<ProductDocument, AeroError>> CreateProductAsync(long tenantId, ProductDocument product, CancellationToken ct = default)
    {
        product.Id = Snowflake.NewId(); product.TenantId = tenantId; product.Version = 0; product.Sku = NormalizeSku(product.Sku); product.CreatedOn = DateTimeOffset.UtcNow;
        var validation = await productValidator.ValidateAsync(product, ct);
        if (!validation.IsValid) return Prelude.Fail<ProductDocument, AeroError>(AeroError.ValidationError(validation.Errors.Select(x => x.ErrorMessage)));
        var duplicate = (await session.Query<ProductDocument>().Where(x => x.TenantId == tenantId && x.Sku == product.Sku).ToListAsync(ct)).Count > 0;
        if (duplicate) return Prelude.Fail<ProductDocument, AeroError>(AeroError.ConflictError("A product with that SKU already exists."));
        return await Store(product, ct);
    }

    public async Task<Result<ProductDocument, AeroError>> UpdateProductAsync(long tenantId, long productId, ProductDocument product, CancellationToken ct = default)
    {
        var existing = await GetProductAsync(tenantId, productId, ct);
        if (existing is not Result<ProductDocument?, AeroError>.Ok(var entity) || entity is null) return Prelude.Fail<ProductDocument, AeroError>(AeroError.NotFoundError("Product not found."));
        if (product.Version <= 0 || product.Version != entity.Version)
            return Prelude.Fail<ProductDocument, AeroError>(AeroError.ConflictError("Product changed since it was loaded. Reload it and try again."));
        var sku = NormalizeSku(product.Sku);
        product.Sku = sku;
        var validation = await productValidator.ValidateAsync(product, ct);
        if (!validation.IsValid) return Prelude.Fail<ProductDocument, AeroError>(AeroError.ValidationError(validation.Errors.Select(x => x.ErrorMessage)));
        var duplicate = (await session.Query<ProductDocument>().Where(x => x.TenantId == tenantId && x.Sku == sku && x.Id != productId).ToListAsync(ct)).Count > 0;
        if (duplicate) return Prelude.Fail<ProductDocument, AeroError>(AeroError.ConflictError("A product with that SKU already exists."));
        entity.Name = product.Name; entity.Description = product.Description; entity.Sku = sku; entity.StockQuantity = product.StockQuantity; entity.IsActive = product.IsActive; entity.Attributes = product.Attributes; entity.Tags = product.Tags; entity.ModifiedOn = DateTimeOffset.UtcNow;
        return await Store(entity, ct);
    }

    public async Task<Result<bool, AeroError>> DeleteProductAsync(long tenantId, long productId, CancellationToken ct = default)
    {
        var existing = await GetProductAsync(tenantId, productId, ct);
        if (existing is not Result<ProductDocument?, AeroError>.Ok(var entity) || entity is null) return Prelude.Ok<bool, AeroError>(false);
        try
        {
            var referenced = await session.Query<ProductListingDocument>()
                .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.ProductId == productId, ct);
            if (referenced is not null)
                return Prelude.Fail<bool, AeroError>(AeroError.ConflictError("Product is referenced by one or more listings. Deactivate it instead."));

            session.Delete(entity);
            await session.SaveChangesAsync(ct);
            return Prelude.Ok<bool, AeroError>(true);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (ConcurrencyException)
        {
            session.ClearChanges();
            return Prelude.Fail<bool, AeroError>(AeroError.ConflictError("Product changed while it was being deleted. Reload and try again."));
        }
        catch (Exception)
        {
            session.ClearChanges();
            return Prelude.Fail<bool, AeroError>(AeroError.DatabaseError("Product could not be deleted."));
        }
    }

    public async Task<Result<ProductListingDocument, AeroError>> CreateListingAsync(long tenantId, long siteId, ProductListingDocument listing, CancellationToken ct = default)
    {
        var product = await LoadTenantProductAsync(listing.ProductId, tenantId, ct);
        if (product is null) return Prelude.Fail<ProductListingDocument, AeroError>(AeroError.NotFoundError("Product not found."));
        if (!TryNormalizeCulture(listing.Culture, out var culture))
            return Prelude.Fail<ProductListingDocument, AeroError>(AeroError.ValidationError(["Culture is invalid."]));
        listing.Id = Snowflake.NewId(); listing.TenantId = tenantId; listing.SiteId = siteId; listing.Version = 0; listing.Currency = "USD"; listing.Culture = culture; listing.Slug = CatalogSlug.Normalize(listing.Slug); listing.CreatedOn = DateTimeOffset.UtcNow;
        var validation = await listingValidator.ValidateAsync(listing, ct);
        if (!validation.IsValid) return Prelude.Fail<ProductListingDocument, AeroError>(AeroError.ValidationError(validation.Errors.Select(x => x.ErrorMessage)));
        var duplicate = (await session.Query<ProductListingDocument>().Where(x => x.SiteId == siteId && x.Culture == listing.Culture && (x.Slug == listing.Slug || x.ProductId == listing.ProductId)).ToListAsync(ct)).Count > 0;
        if (duplicate) return Prelude.Fail<ProductListingDocument, AeroError>(AeroError.ConflictError("That site and culture already has this slug or product listing."));
        return await StoreListingWithProductAsync(product, listing, ct);
    }

    public async Task<Result<ProductListingDocument, AeroError>> UpdateListingAsync(long tenantId, long siteId, long listingId, ProductListingDocument listing, CancellationToken ct = default)
    {
        var existing = await session.Query<ProductListingDocument>().FirstOrDefaultAsync(x => x.Id == listingId && x.TenantId == tenantId && x.SiteId == siteId, ct);
        if (existing is null) return Prelude.Fail<ProductListingDocument, AeroError>(AeroError.NotFoundError("Listing not found."));
        if (listing.Version <= 0 || listing.Version != existing.Version)
            return Prelude.Fail<ProductListingDocument, AeroError>(AeroError.ConflictError("Listing changed since it was loaded. Reload it and try again."));
        var product = await LoadTenantProductAsync(listing.ProductId, tenantId, ct);
        if (product is null) return Prelude.Fail<ProductListingDocument, AeroError>(AeroError.NotFoundError("Listing not found."));
        if (!TryNormalizeCulture(listing.Culture, out var culture))
            return Prelude.Fail<ProductListingDocument, AeroError>(AeroError.ValidationError(["Culture is invalid."]));
        var slug = CatalogSlug.Normalize(listing.Slug);
        listing.Culture = culture; listing.Slug = slug; listing.Currency = "USD";
        var validation = await listingValidator.ValidateAsync(listing, ct);
        if (!validation.IsValid) return Prelude.Fail<ProductListingDocument, AeroError>(AeroError.ValidationError(validation.Errors.Select(x => x.ErrorMessage)));
        var duplicate = (await session.Query<ProductListingDocument>().Where(x => x.SiteId == siteId && x.Culture == listing.Culture && x.Id != listingId && (x.Slug == slug || x.ProductId == listing.ProductId)).ToListAsync(ct)).Count > 0;
        if (duplicate) return Prelude.Fail<ProductListingDocument, AeroError>(AeroError.ConflictError("That site and culture already has this slug or product listing."));
        existing.ProductId = listing.ProductId; existing.Culture = listing.Culture; existing.Slug = slug; existing.Name = listing.Name; existing.ShortDescription = listing.ShortDescription; existing.Description = listing.Description; existing.Category = listing.Category; existing.ImageUrl = listing.ImageUrl; existing.Price = listing.Price; existing.CompareAtPrice = listing.CompareAtPrice; existing.IsPublished = listing.IsPublished; existing.IsFeatured = listing.IsFeatured; existing.Currency = "USD"; existing.ModifiedOn = DateTimeOffset.UtcNow;
        return await StoreListingWithProductAsync(product, existing, ct);
    }

    public async Task<Result<bool, AeroError>> DeleteListingAsync(long tenantId, long siteId, long listingId, CancellationToken ct = default)
    {
        var listing = await session.Query<ProductListingDocument>().FirstOrDefaultAsync(x => x.Id == listingId && x.TenantId == tenantId && x.SiteId == siteId, ct);
        if (listing is null) return Prelude.Ok<bool, AeroError>(false);
        try { session.Delete(listing); await session.SaveChangesAsync(ct); return Prelude.Ok<bool, AeroError>(true); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (ConcurrencyException) { session.ClearChanges(); return Prelude.Fail<bool, AeroError>(AeroError.ConflictError("Listing changed while it was being deleted. Reload and try again.")); }
        catch (Exception) { session.ClearChanges(); return Prelude.Fail<bool, AeroError>(AeroError.DatabaseError("Listing could not be deleted.")); }
    }

    private async Task<ProductDocument?> LoadTenantProductAsync(long productId, long tenantId, CancellationToken ct)
    {
        return await session.Query<ProductDocument>()
            .FirstOrDefaultAsync(x => x.Id == productId && x.TenantId == tenantId, ct);
    }

    private async Task<Result<ProductListingDocument, AeroError>> StoreListingWithProductAsync(
        ProductDocument product,
        ProductListingDocument listing,
        CancellationToken ct)
    {
        try
        {
            product.ModifiedOn = DateTimeOffset.UtcNow;
            session.Store(product);
            session.Store(listing);
            await session.SaveChangesAsync(ct);
            return Prelude.Ok<ProductListingDocument, AeroError>(listing);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (ConcurrencyException)
        {
            session.ClearChanges();
            return Prelude.Fail<ProductListingDocument, AeroError>(AeroError.ConflictError("Product or listing changed while the listing was being saved. Reload and try again."));
        }
        catch (Exception exception) when (IsUniqueConstraintConflict(exception))
        {
            session.ClearChanges();
            return Prelude.Fail<ProductListingDocument, AeroError>(AeroError.ConflictError("That site and culture already has this slug or product listing."));
        }
        catch (Exception)
        {
            session.ClearChanges();
            return Prelude.Fail<ProductListingDocument, AeroError>(AeroError.DatabaseError("Catalog changes could not be saved."));
        }
    }

    private async Task<Result<T, AeroError>> Store<T>(T document, CancellationToken ct) where T : class
    {
        try
        {
            session.Store(document);
            await session.SaveChangesAsync(ct);
            return Prelude.Ok<T, AeroError>(document);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (ConcurrencyException)
        {
            session.ClearChanges();
            return Prelude.Fail<T, AeroError>(AeroError.ConflictError("Catalog data changed while it was being saved. Reload and try again."));
        }
        catch (Exception exception) when (IsUniqueConstraintConflict(exception))
        {
            session.ClearChanges();
            return Prelude.Fail<T, AeroError>(AeroError.ConflictError("Catalog data conflicts with an existing record."));
        }
        catch (Exception)
        {
            session.ClearChanges();
            return Prelude.Fail<T, AeroError>(AeroError.DatabaseError("Catalog changes could not be saved."));
        }
    }
    private static async Task<Result<T?, AeroError>> Execute<T>(Func<Task<T?>> action) where T : class { try { return Prelude.Ok<T?, AeroError>(await action()); } catch (OperationCanceledException) { throw; } catch (Exception) { return Prelude.Fail<T?, AeroError>(AeroError.DatabaseError("Catalog data could not be loaded.")); } }
    private static string NormalizeSku(string? sku) => (sku ?? string.Empty).Trim().ToUpperInvariant();
    private static bool TryNormalizeCulture(string? culture, out string canonical)
    {
        try
        {
            canonical = CultureInfo.GetCultureInfo((culture ?? string.Empty).Trim()).Name;
            return !string.IsNullOrWhiteSpace(canonical);
        }
        catch (CultureNotFoundException)
        {
            canonical = string.Empty;
            return false;
        }
    }

    internal static bool IsUniqueConstraintConflict(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            var message = current.Message;
            if ((message.Contains("unique", StringComparison.OrdinalIgnoreCase) &&
                 (message.Contains("index", StringComparison.OrdinalIgnoreCase) ||
                  message.Contains("constraint", StringComparison.OrdinalIgnoreCase))) ||
                (message.Contains("Database index `uidx_", StringComparison.OrdinalIgnoreCase) &&
                 message.Contains("already contains", StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        return false;
    }

    private sealed class CatalogCountRow
    {
        [JsonPropertyName("total_count")]
        public long TotalCount { get; set; }
    }
}
