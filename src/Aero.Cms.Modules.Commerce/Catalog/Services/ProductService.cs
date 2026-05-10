using Aero.Cms.Modules.Commerce.Catalog.Models;
using Marten;
using Marten.Linq;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Commerce.Catalog.Services;

public sealed class ProductService(IDocumentSession session, ILogger<ProductService> log)
    : GenericMartenRepository<ProductDocument>(session, log), IProductService
{
    public async Task<Result<ProductDocument?, AeroError>> FindBySlugAsync(string slug, CancellationToken ct = default)
    {
        try
        {
            var product = await session.Query<ProductDocument>()
                .FirstOrDefaultAsync(x => x.Slug == slug, token: ct);

            return product is null
                ? Prelude.Fail<ProductDocument?, AeroError>(AeroError.CreateError($"Product with slug '{slug}' not found"))
                : Prelude.Ok<ProductDocument?, AeroError>(product);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<ProductDocument?, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

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

            return Prelude.Ok<(IReadOnlyList<ProductDocument>, long), AeroError>((items, stats.TotalResults));
        }
        catch (Exception ex)
        {
            return Prelude.Fail<(IReadOnlyList<ProductDocument>, long), AeroError>(AeroError.CreateError(ex.Message));
        }
    }
}
