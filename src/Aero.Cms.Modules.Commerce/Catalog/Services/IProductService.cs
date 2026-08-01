using Aero.Cms.Modules.Commerce.Catalog.Models;

namespace Aero.Cms.Modules.Commerce.Catalog.Services;

/// <summary>Provides tenant-scoped canonical products and site-scoped storefront listings.</summary>
public interface IProductService
{
    Task<Result<ProductDocument?, AeroError>> GetProductAsync(long tenantId, long productId, CancellationToken ct = default);
    Task<Result<(IReadOnlyList<ProductDocument> Items, long TotalCount), AeroError>> SearchProductsAsync(long tenantId, string? search = null, int skip = 0, int take = 20, CancellationToken ct = default);
    Task<Result<ProductListingDocument?, AeroError>> GetListingAsync(long tenantId, long siteId, long listingId, CancellationToken ct = default);
    Task<Result<(IReadOnlyList<ProductListingDocument> Items, long TotalCount), AeroError>> SearchListingsAsync(long tenantId, long siteId, string? culture = null, string? search = null, int skip = 0, int take = 20, CancellationToken ct = default);
    Task<Result<ProductListingDocument?, AeroError>> GetPublishedListingBySlugAsync(long tenantId, long siteId, string culture, string slug, CancellationToken ct = default);
    Task<Result<ProductListingDocument?, AeroError>> GetPublishedListingAsync(long tenantId, long siteId, string culture, long listingId, CancellationToken ct = default);
    Task<Result<(IReadOnlyList<ProductListingDocument> Items, long TotalCount), AeroError>> SearchPublishedAsync(long tenantId, long siteId, string culture, string? search = null, string? category = null, int skip = 0, int take = 20, bool featuredOnly = false, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ProductListingDocument>, AeroError>> GetRecentPublishedAsync(long tenantId, long siteId, string culture, int take = 6, CancellationToken ct = default);
    Task<Result<IReadOnlyList<string>, AeroError>> GetPublishedCategoriesAsync(long tenantId, long siteId, string culture, CancellationToken ct = default);
    Task<Result<ProductDocument, AeroError>> CreateProductAsync(long tenantId, ProductDocument product, CancellationToken ct = default);
    Task<Result<ProductDocument, AeroError>> UpdateProductAsync(long tenantId, long productId, ProductDocument product, CancellationToken ct = default);
    Task<Result<bool, AeroError>> DeleteProductAsync(long tenantId, long productId, CancellationToken ct = default);
    Task<Result<ProductListingDocument, AeroError>> CreateListingAsync(long tenantId, long siteId, ProductListingDocument listing, CancellationToken ct = default);
    Task<Result<ProductListingDocument, AeroError>> UpdateListingAsync(long tenantId, long siteId, long listingId, ProductListingDocument listing, CancellationToken ct = default);
    Task<Result<bool, AeroError>> DeleteListingAsync(long tenantId, long siteId, long listingId, CancellationToken ct = default);
}
