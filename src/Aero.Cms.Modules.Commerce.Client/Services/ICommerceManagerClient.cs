using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Modules.Commerce.Client.Services;

/// <summary>Selected-site manager operations for products and storefront listings.</summary>
public interface ICommerceManagerClient
{
    Task<Result<ManagerCatalogPage<ManagerProductDto>, AeroError>> GetProductsAsync(string? search, int skip, int take, CancellationToken ct = default);
    Task<Result<ManagerProductDto, AeroError>> GetProductAsync(long id, CancellationToken ct = default);
    Task<Result<ManagerProductDto, AeroError>> CreateProductAsync(ManagerProductRequest request, CancellationToken ct = default);
    Task<Result<ManagerProductDto, AeroError>> UpdateProductAsync(long id, ManagerProductRequest request, CancellationToken ct = default);
    Task<Result<bool, AeroError>> DeleteProductAsync(long id, CancellationToken ct = default);
    Task<Result<ManagerCatalogPage<ManagerListingDto>, AeroError>> GetListingsAsync(string? culture, string? search, int skip, int take, CancellationToken ct = default);
    Task<Result<ManagerListingDto, AeroError>> GetListingAsync(long id, CancellationToken ct = default);
    Task<Result<ManagerListingDto, AeroError>> CreateListingAsync(ManagerListingRequest request, CancellationToken ct = default);
    Task<Result<ManagerListingDto, AeroError>> UpdateListingAsync(long id, ManagerListingRequest request, CancellationToken ct = default);
    Task<Result<bool, AeroError>> DeleteListingAsync(long id, CancellationToken ct = default);
}

public sealed record ManagerCatalogPage<T>(IReadOnlyList<T> Items, long TotalCount);
public sealed record ManagerProductDto(long Id, string Name, string? Description, string Sku, int StockQuantity, bool IsActive, IReadOnlyDictionary<string, string> Attributes, IReadOnlyList<string> Tags, long Version);
public sealed record ManagerListingDto(long Id, long ProductId, string Culture, string Slug, string Name, string? ShortDescription, string? Description, string? Category, string? ImageUrl, decimal Price, decimal? CompareAtPrice, string Currency, bool IsPublished, bool IsFeatured, long Version);
public sealed record ManagerProductRequest(string Name, string? Description, string Sku, int StockQuantity, bool IsActive, Dictionary<string, string> Attributes, List<string> Tags, long Version);
public sealed record ManagerListingRequest(long ProductId, string Culture, string Slug, string Name, string? ShortDescription, string? Description, string? Category, string? ImageUrl, decimal Price, decimal? CompareAtPrice, bool IsPublished, bool IsFeatured, long Version);
