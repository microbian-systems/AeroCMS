using System.Linq.Expressions;
using Aero.Cms.Modules.Commerce.Catalog.Models;

namespace Aero.Cms.Modules.Commerce.Catalog.Services;

/// <summary>
/// Provides document-store access to catalog products and culture-aware product reads.
/// </summary>
/// <remarks>
/// Read methods apply translations for the current UI culture when available. Mutating methods commit the current
/// document session; they do not perform validation, enforce unique slugs or SKUs, scope by tenant or site, or
/// coordinate concurrent updates.
/// </remarks>
public interface IProductService
{
    /// <summary>Loads a product by document identifier and applies its current-culture translation when available.</summary>
    Task<Result<ProductDocument?, AeroError>> GetByIdAsync(long id, CancellationToken ct = default);
    /// <summary>Lists all products visible to the session and applies current-culture translations.</summary>
    Task<Result<IReadOnlyList<ProductDocument>, AeroError>> GetAllAsync(CancellationToken ct = default);
    /// <summary>Queries products with a provider-translatable predicate and applies current-culture translations.</summary>
    Task<Result<IReadOnlyList<ProductDocument>, AeroError>> FindAsync(Expression<Func<ProductDocument, bool>> predicate, CancellationToken ct = default);
    /// <summary>Stores a product and commits the current document session.</summary>
    Task<Result<ProductDocument, AeroError>> InsertAsync(ProductDocument entity, CancellationToken ct = default);
    /// <summary>Stores a product and commits the current document session.</summary>
    Task<Result<ProductDocument, AeroError>> UpdateAsync(ProductDocument entity, CancellationToken ct = default);
    /// <summary>Deletes the supplied product identifier and commits the current document session.</summary>
    Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default);
    /// <summary>Counts products visible to the current document session.</summary>
    Task<Result<long, AeroError>> CountAsync(CancellationToken ct = default);

    /// <summary>Finds the first product whose slug exactly matches the supplied value and applies a current-culture translation.</summary>
    /// <remarks>A missing product is returned as a failed result rather than a successful null value.</remarks>
    Task<Result<ProductDocument?, AeroError>> FindBySlugAsync(string slug, CancellationToken ct = default);
    /// <summary>Searches and pages products using optional textual, category, and inclusive price filters.</summary>
    /// <remarks>The method does not validate page bounds or currency, and price filters apply to the stored decimal price.</remarks>
    Task<Result<(IReadOnlyList<ProductDocument> Items, long TotalCount), AeroError>> SearchAsync(
        string? search = null,
        string? category = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        int skip = 0,
        int take = 20,
        CancellationToken ct = default);
}
