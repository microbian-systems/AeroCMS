namespace Aero.Cms.Modules.Commerce.Client.Services;

/// <summary>
/// Typed HTTP client for the Commerce module's Minimal APIs.
/// Registered in the WASM client and calls back to the server-hosted endpoints.
/// </summary>
public interface ICommerceClientService
{
    // Catalog
        /// <summary>
    /// GetProductsAsync method.
    /// </summary>
Task<IReadOnlyList<ProductDto>> GetProductsAsync(string? search = null, string? category = null, int skip = 0, int take = 20);
        /// <summary>
    /// GetProductByIdAsync method.
    /// </summary>
Task<ProductDto?> GetProductByIdAsync(long id);
        /// <summary>
    /// GetProductBySlugAsync method.
    /// </summary>
Task<ProductDto?> GetProductBySlugAsync(string slug);
        /// <summary>
    /// CreateProductAsync method.
    /// </summary>
Task<ProductDto?> CreateProductAsync(CreateProductRequest request);
        /// <summary>
    /// UpdateProductAsync method.
    /// </summary>
Task<ProductDto?> UpdateProductAsync(long id, UpdateProductRequest request);
        /// <summary>
    /// DeleteProductAsync method.
    /// </summary>
Task<bool> DeleteProductAsync(long id);

    // Basket
        /// <summary>
    /// GetBasketAsync method.
    /// </summary>
Task<BasketDto?> GetBasketAsync(string customerId);
        /// <summary>
    /// AddItemToBasketAsync method.
    /// </summary>
Task<BasketDto?> AddItemToBasketAsync(string customerId, AddBasketItemRequest request);
        /// <summary>
    /// RemoveItemFromBasketAsync method.
    /// </summary>
Task<BasketDto?> RemoveItemFromBasketAsync(string customerId, long productId);
        /// <summary>
    /// ClearBasketAsync method.
    /// </summary>
Task<BasketDto?> ClearBasketAsync(string customerId);

    // Orders
        /// <summary>
    /// GetOrdersAsync method.
    /// </summary>
Task<IReadOnlyList<OrderDto>> GetOrdersAsync(int skip = 0, int take = 20);
        /// <summary>
    /// GetOrderByIdAsync method.
    /// </summary>
Task<OrderDto?> GetOrderByIdAsync(long id);
}

// --- DTOs (mirror server models, no entity references) ---

/// <summary>
/// Represents a record for ProductDto.
/// </summary>
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

/// <summary>
/// Represents a record for CreateProductRequest.
/// </summary>
public sealed record CreateProductRequest(
    string Name,
    string Slug,
    string? Sku,
    string? Description,
    string? Category,
    decimal Price,
    int StockQuantity
);

/// <summary>
/// Represents a record for UpdateProductRequest.
/// </summary>
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
/// <summary>
/// Represents a record for BasketDto.
/// </summary>
public sealed record BasketDto(
    long Id,
    string CustomerId,
    decimal TotalAmount,
    string Currency,
    List<BasketItemDto> Items
);

/// <summary>
/// Represents a record for BasketItemDto.
/// </summary>
public sealed record BasketItemDto(
    long ProductId,
    string ProductName,
    string? Sku,
    string? ImageUrl,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice
);

/// <summary>
/// Represents a record for AddBasketItemRequest.
/// </summary>
public sealed record AddBasketItemRequest(
    long ProductId,
    string ProductName,
    string? Sku,
    string? ImageUrl,
    int Quantity,
    decimal UnitPrice
);

// Order DTOs
/// <summary>
/// Represents a record for OrderDto.
/// </summary>
public sealed record OrderDto(
    long Id,
    string? CustomerId,
    string Status,
    decimal TotalAmount,
    DateTimeOffset CreatedOn,
    List<OrderItemDto> Items
);

/// <summary>
/// Represents a record for OrderItemDto.
/// </summary>
public sealed record OrderItemDto(
    long ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice
);
