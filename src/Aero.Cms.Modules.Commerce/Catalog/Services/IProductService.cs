using Aero.Cms.Modules.Commerce.Catalog.Models;

namespace Aero.Cms.Modules.Commerce.Catalog.Services;

public interface IProductService : IGenericMartenRepository<ProductDocument>
{
    Task<Result<ProductDocument?, AeroError>> FindBySlugAsync(string slug, CancellationToken ct = default);
    Task<Result<(IReadOnlyList<ProductDocument> Items, long TotalCount), AeroError>> SearchAsync(
        string? search = null,
        string? category = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        int skip = 0,
        int take = 20,
        CancellationToken ct = default);
}
