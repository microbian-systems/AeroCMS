namespace Aero.Cms.Modules.Commerce.Client.Services;

/// <summary>
/// Typed HTTP client for the Commerce module's Minimal APIs.
/// Registered in the WASM client and calls back to the server-hosted endpoints.
/// </summary>
public interface ICommerceClientService
{
    // Catalog
    Task<IReadOnlyList<ProductDto>> GetProductsAsync(string? search = null, string? category = null, int skip = 0, int take = 20);
    Task<ProductDto?> GetProductByIdAsync(long id);
    Task<ProductDto?> GetProductBySlugAsync(string slug);
    Task<ProductDto?> CreateProductAsync(CreateProductRequest request);
    Task<ProductDto?> UpdateProductAsync(long id, UpdateProductRequest request);
    Task<bool> DeleteProductAsync(long id);

    // Basket
    Task<BasketDto?> GetBasketAsync(string customerId);
    Task<BasketDto?> AddItemToBasketAsync(string customerId, AddBasketItemRequest request);
    Task<BasketDto?> RemoveItemFromBasketAsync(string customerId, long productId);
    Task<BasketDto?> ClearBasketAsync(string customerId);

    // Orders
    Task<IReadOnlyList<OrderDto>> GetOrdersAsync(int skip = 0, int take = 20);
    Task<OrderDto?> GetOrderByIdAsync(long id);
}

// --- DTOs (mirror server models, no entity references) ---

public sealed record ProductDto(
    long Id,
    string Name,
    string Slug,
    string? Sku,
    string? Description,
    string? Category,
    decimal Price,
    int StockQuantity,
    bool IsPublished,
    string? ImageUrl
);

public sealed record CreateProductRequest(
    string Name,
    string Slug,
    string? Sku,
    string? Description,
    string? Category,
    decimal Price,
    int StockQuantity
);

public sealed record UpdateProductRequest(
    string Name,
    string Slug,
    string? Description,
    string? Category,
    decimal Price,
    int StockQuantity,
    bool IsPublished
);

// Basket DTOs
public sealed record BasketDto(
    long Id,
    string CustomerId,
    decimal TotalAmount,
    string Currency,
    List<BasketItemDto> Items
);

public sealed record BasketItemDto(
    long ProductId,
    string ProductName,
    string? Sku,
    string? ImageUrl,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice
);

public sealed record AddBasketItemRequest(
    long ProductId,
    string ProductName,
    string? Sku,
    string? ImageUrl,
    int Quantity,
    decimal UnitPrice
);

// Order DTOs
public sealed record OrderDto(
    long Id,
    string? CustomerId,
    string Status,
    decimal TotalAmount,
    DateTimeOffset CreatedOn,
    List<OrderItemDto> Items
);

public sealed record OrderItemDto(
    long ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice
);
