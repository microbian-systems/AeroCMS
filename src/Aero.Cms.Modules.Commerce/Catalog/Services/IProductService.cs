using System.Linq.Expressions;
using Aero.Cms.Modules.Commerce.Catalog.Models;

namespace Aero.Cms.Modules.Commerce.Catalog.Services;

/// <summary>
/// Defines an interface for IProductService.
/// </summary>
public interface IProductService
{
        /// <summary>
    /// GetByIdAsync method.
    /// </summary>
Task<Result<ProductDocument?, AeroError>> GetByIdAsync(long id, CancellationToken ct = default);
        /// <summary>
    /// GetAllAsync method.
    /// </summary>
Task<Result<IReadOnlyList<ProductDocument>, AeroError>> GetAllAsync(CancellationToken ct = default);
        /// <summary>
    /// FindAsync method.
    /// </summary>
Task<Result<IReadOnlyList<ProductDocument>, AeroError>> FindAsync(Expression<Func<ProductDocument, bool>> predicate, CancellationToken ct = default);
        /// <summary>
    /// InsertAsync method.
    /// </summary>
Task<Result<ProductDocument, AeroError>> InsertAsync(ProductDocument entity, CancellationToken ct = default);
        /// <summary>
    /// UpdateAsync method.
    /// </summary>
Task<Result<ProductDocument, AeroError>> UpdateAsync(ProductDocument entity, CancellationToken ct = default);
        /// <summary>
    /// DeleteAsync method.
    /// </summary>
Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default);
        /// <summary>
    /// CountAsync method.
    /// </summary>
Task<Result<long, AeroError>> CountAsync(CancellationToken ct = default);

        /// <summary>
    /// FindBySlugAsync method.
    /// </summary>
Task<Result<ProductDocument?, AeroError>> FindBySlugAsync(string slug, CancellationToken ct = default);
        /// <summary>
    /// SearchAsync method.
    /// </summary>
Task<Result<(IReadOnlyList<ProductDocument> Items, long TotalCount), AeroError>> SearchAsync(
        string? search = null,
        string? category = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        int skip = 0,
        int take = 20,
        CancellationToken ct = default);
}
