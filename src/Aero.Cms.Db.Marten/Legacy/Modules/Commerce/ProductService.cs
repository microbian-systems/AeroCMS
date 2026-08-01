using System.Globalization;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Commerce.Catalog.Models;
using Marten;
using Marten.Linq;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Commerce.Catalog.Services;

/// <summary>
/// Represents a class for ProductService.
/// </summary>
public sealed class ProductService(IDocumentSession session, ILogger<ProductService> log)
    : GenericMartenRepository<ProductDocument>(session, log), IProductService
{
        /// <summary>
    /// FindBySlugAsync method.
    /// </summary>
public async Task<Result<ProductDocument?, AeroError>> FindBySlugAsync(string slug, CancellationToken ct = default)
    {
        try
        {
            var product = await session.Query<ProductDocument>()
                .FirstOrDefaultAsync(x => x.Slug == slug, token: ct);

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
            var martenQueryable = (IMartenQueryable<ProductDocument>)session.Query<ProductDocument>();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.ToLowerInvariant();
                martenQueryable = (IMartenQueryable<ProductDocument>)martenQueryable.Where(x =>
                    x.Name.ToLower().Contains(term) ||
                    x.Description!.ToLower().Contains(term) ||
                    x.Sku!.ToLower().Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(category))
                martenQueryable = (IMartenQueryable<ProductDocument>)martenQueryable.Where(x => x.Category == category);

            if (minPrice.HasValue)
                martenQueryable = (IMartenQueryable<ProductDocument>)martenQueryable.Where(x => x.Price >= minPrice.Value);

            if (maxPrice.HasValue)
                martenQueryable = (IMartenQueryable<ProductDocument>)martenQueryable.Where(x => x.Price <= maxPrice.Value);

            var stats = new QueryStatistics();
            var items = await martenQueryable
                .Stats(out stats)
                .Skip(skip)
                .Take(take)
                .ToListAsync(token: ct);

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
        var translations = await session.Query<ProductTranslation>()
            .Where(x => x.Culture == culture && productIds.Contains(x.ProductId))
            .ToListAsync(token: ct);

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

        var translation = await session.Query<ProductTranslation>()
            .FirstOrDefaultAsync(x => x.ProductId == product.Id && x.Culture == culture, token: ct);

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
