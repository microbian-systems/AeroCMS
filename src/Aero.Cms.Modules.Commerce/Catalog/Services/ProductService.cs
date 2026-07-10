using System.Globalization;
using System.Linq.Expressions;
using Aero.Cms.Modules.Commerce.Catalog.Models;
using AeroDB.Sable;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Commerce.Catalog.Services;

/// <summary>
/// Represents a class for ProductService.
/// </summary>
public sealed class ProductService(IDocumentSession docSession, ILogger<ProductService> log)
    : IProductService
{
        /// <summary>
    /// GetByIdAsync method.
    /// </summary>
public async Task<Result<ProductDocument?, AeroError>> GetByIdAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var product = await docSession.LoadAsync<ProductDocument>(id, ct);
            if (product is not null)
                await ApplyTranslationAsync(product, GetCurrentCulture(), ct);

            return product is null
                ? Prelude.Ok<ProductDocument?, AeroError>(null)
                : Prelude.Ok<ProductDocument?, AeroError>(product);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<ProductDocument?, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

        /// <summary>
    /// GetAllAsync method.
    /// </summary>
public async Task<Result<IReadOnlyList<ProductDocument>, AeroError>> GetAllAsync(CancellationToken ct = default)
    {
        try
        {
            var items = await docSession.Query<ProductDocument>().ToListAsync(ct);
            await ApplyTranslationsAsync(items, GetCurrentCulture(), ct);
            return Prelude.Ok<IReadOnlyList<ProductDocument>, AeroError>(items);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<IReadOnlyList<ProductDocument>, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

        /// <summary>
    /// FindAsync method.
    /// </summary>
public async Task<Result<IReadOnlyList<ProductDocument>, AeroError>> FindAsync(
        Expression<Func<ProductDocument, bool>> predicate, CancellationToken ct = default)
    {
        try
        {
            var items = await docSession.Query<ProductDocument>().Where(predicate).ToListAsync(ct);
            await ApplyTranslationsAsync(items, GetCurrentCulture(), ct);
            return Prelude.Ok<IReadOnlyList<ProductDocument>, AeroError>(items);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<IReadOnlyList<ProductDocument>, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

        /// <summary>
    /// InsertAsync method.
    /// </summary>
public async Task<Result<ProductDocument, AeroError>> InsertAsync(ProductDocument entity, CancellationToken ct = default)
    {
        try
        {
            docSession.Store(entity);
            await docSession.SaveChangesAsync(ct);
            return Prelude.Ok<ProductDocument, AeroError>(entity);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<ProductDocument, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

        /// <summary>
    /// UpdateAsync method.
    /// </summary>
public async Task<Result<ProductDocument, AeroError>> UpdateAsync(ProductDocument entity, CancellationToken ct = default)
    {
        try
        {
            docSession.Store(entity);
            await docSession.SaveChangesAsync(ct);
            return Prelude.Ok<ProductDocument, AeroError>(entity);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<ProductDocument, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

        /// <summary>
    /// DeleteAsync method.
    /// </summary>
public async Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default)
    {
        try
        {
            docSession.Delete<ProductDocument>(id);
            await docSession.SaveChangesAsync(ct);
            return Prelude.Ok<bool, AeroError>(true);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<bool, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

        /// <summary>
    /// CountAsync method.
    /// </summary>
public async Task<Result<long, AeroError>> CountAsync(CancellationToken ct = default)
    {
        try
        {
            var count = await docSession.Query<ProductDocument>().CountAsync(ct);
            return Prelude.Ok<long, AeroError>(count);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<long, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

        /// <summary>
    /// FindBySlugAsync method.
    /// </summary>
public async Task<Result<ProductDocument?, AeroError>> FindBySlugAsync(string slug, CancellationToken ct = default)
    {
        try
        {
            var product = await docSession.Query<ProductDocument>()
                .FirstOrDefaultAsync(x => x.Slug == slug, ct);

            if (product is not null)
                await ApplyTranslationAsync(product, GetCurrentCulture(), ct);

            return product is null
                ? Prelude.Fail<ProductDocument?, AeroError>(AeroError.CreateError($"Product with slug '{slug}' not found"))
                : Prelude.Ok<ProductDocument?, AeroError>(product);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<ProductDocument?, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

        /// <summary>
    /// SearchAsync method.
    /// </summary>
public async Task<Result<(IReadOnlyList<ProductDocument> Items, long TotalCount), AeroError>> SearchAsync(
        string? search = null,
        string? category = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        int skip = 0,
        int take = 20,
        CancellationToken ct = default)
    {
        try
        {
            var martenQueryable = (ISurrealDbQueryable<ProductDocument>)docSession.Query<ProductDocument>();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.ToLowerInvariant();
                martenQueryable = (ISurrealDbQueryable<ProductDocument>)martenQueryable.Where(x =>
                    x.Name.ToLower().Contains(term) ||
                    x.Description!.ToLower().Contains(term) ||
                    x.Sku!.ToLower().Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(category))
                martenQueryable = (ISurrealDbQueryable<ProductDocument>)martenQueryable.Where(x => x.Category == category);

            if (minPrice.HasValue)
                martenQueryable = (ISurrealDbQueryable<ProductDocument>)martenQueryable.Where(x => x.Price >= minPrice.Value);

            if (maxPrice.HasValue)
                martenQueryable = (ISurrealDbQueryable<ProductDocument>)martenQueryable.Where(x => x.Price <= maxPrice.Value);

            var stats = new QueryStatistics();
            var items = await martenQueryable
                .Stats(out stats)
                .Skip(skip)
                .Take(take)
                .ToListAsync(ct);

            await ApplyTranslationsAsync(items, GetCurrentCulture(), ct);

            return Prelude.Ok<(IReadOnlyList<ProductDocument>, long), AeroError>((items, stats.TotalResults));
        }
        catch (Exception ex)
        {
            return Prelude.Fail<(IReadOnlyList<ProductDocument>, long), AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    private async Task ApplyTranslationsAsync(
        IReadOnlyList<ProductDocument> products,
        string culture,
        CancellationToken ct)
    {
        if (products.Count == 0 || string.Equals(culture, SitesModel.DefaultCultureName, StringComparison.OrdinalIgnoreCase))
            return;

        var productIds = products.Select(x => x.Id).ToArray();
        var translations = await docSession.Query<ProductTranslation>()
            .Where(x => x.Culture == culture && productIds.Contains(x.ProductId))
            .ToListAsync(ct);

        var translationsByProductId = translations
            .GroupBy(x => x.ProductId)
            .ToDictionary(x => x.Key, x => x.First());

        foreach (var product in products)
        {
            if (translationsByProductId.TryGetValue(product.Id, out var translation))
                ProductTranslationMapper.Apply(product, translation);
        }
    }

    private async Task ApplyTranslationAsync(ProductDocument product, string culture, CancellationToken ct)
    {
        if (string.Equals(culture, SitesModel.DefaultCultureName, StringComparison.OrdinalIgnoreCase))
            return;

        var translation = await docSession.Query<ProductTranslation>()
            .FirstOrDefaultAsync(x => x.ProductId == product.Id && x.Culture == culture, ct);

        if (translation is not null)
            ProductTranslationMapper.Apply(product, translation);
    }

    private static string GetCurrentCulture()
    {
        try
        {
            return CultureInfo.GetCultureInfo(CultureInfo.CurrentUICulture.Name).Name;
        }
        catch (CultureNotFoundException)
        {
            return SitesModel.DefaultCultureName;
        }
    }
}
