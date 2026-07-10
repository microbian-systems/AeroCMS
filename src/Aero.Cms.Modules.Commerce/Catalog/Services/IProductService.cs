using System.Linq.Expressions;
using Aero.Cms.Modules.Commerce.Catalog.Models;

namespace Aero.Cms.Modules.Commerce.Catalog.Services;

public interface IProductService
{
    Task<Result<ProductDocument?, AeroError>> GetByIdAsync(long id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ProductDocument>, AeroError>> GetAllAsync(CancellationToken ct = default);
    Task<Result<IReadOnlyList<ProductDocument>, AeroError>> FindAsync(Expression<Func<ProductDocument, bool>> predicate, CancellationToken ct = default);
    Task<Result<ProductDocument, AeroError>> InsertAsync(ProductDocument entity, CancellationToken ct = default);
    Task<Result<ProductDocument, AeroError>> UpdateAsync(ProductDocument entity, CancellationToken ct = default);
    Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default);
    Task<Result<long, AeroError>> CountAsync(CancellationToken ct = default);

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
