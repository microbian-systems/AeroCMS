namespace Aero.Cms.Modules.Commerce.Client.Services;

/// <summary>
/// Typed client for the Commerce HTTP endpoints.
/// </summary>
/// <remarks>
/// Methods use the caller-configured <see cref="HttpClient"/> authentication, timeout, and retry behavior.
/// This contract supplies no cancellation tokens, client-side validation, idempotency keys, or server guarantees.
/// Basket methods transmit the supplied customer identifier as a query parameter; the client does not verify ownership.
/// </remarks>
public interface ICommerceClientService
{
    // Catalog
        /// <summary>
    /// Gets catalog products from <c>GET /api/commerce/catalog/products</c>.
    /// </summary>
Task<IReadOnlyList<ProductDto>> GetProductsAsync(string? search = null, string? category = null, int skip = 0, int take = 20);
        /// <summary>
    /// Gets a product from <c>GET /api/commerce/catalog/products/{id}</c>.
    /// </summary>
Task<ProductDto?> GetProductByIdAsync(long id);
        /// <summary>
    /// Gets a product from <c>GET /api/commerce/catalog/products/by-slug/{slug}</c>.
    /// </summary>
Task<ProductDto?> GetProductBySlugAsync(string slug);
        /// <summary>
    /// Posts a JSON product request to <c>/api/commerce/catalog/products</c>.
    /// </summary>
Task<ProductDto?> CreateProductAsync(CreateProductRequest request);
        /// <summary>
    /// Puts a JSON product request to <c>/api/commerce/catalog/products/{id}</c>.
    /// </summary>
Task<ProductDto?> UpdateProductAsync(long id, UpdateProductRequest request);
        /// <summary>
    /// Deletes <c>/api/commerce/catalog/products/{id}</c> and returns the HTTP success status.
    /// </summary>
Task<bool> DeleteProductAsync(long id);

    // Basket
        /// <summary>
    /// Gets <c>/api/commerce/basket</c> for the supplied query-string customer identifier.
    /// </summary>
Task<BasketDto?> GetBasketAsync(string customerId);
        /// <summary>
    /// Posts a JSON basket item to <c>/api/commerce/basket/items</c> for the supplied customer identifier.
    /// </summary>
Task<BasketDto?> AddItemToBasketAsync(string customerId, AddBasketItemRequest request);
        /// <summary>
    /// Deletes a product from the supplied customer's basket.
    /// </summary>
Task<BasketDto?> RemoveItemFromBasketAsync(string customerId, long productId);
        /// <summary>
    /// Deletes the supplied customer's basket endpoint.
    /// </summary>
Task<BasketDto?> ClearBasketAsync(string customerId);

    // Orders
        /// <summary>
    /// Gets orders from <c>GET /api/commerce/orders</c> with skip/take query values.
    /// </summary>
Task<IReadOnlyList<OrderDto>> GetOrdersAsync(int skip = 0, int take = 20);
        /// <summary>
    /// Gets an order from <c>GET /api/commerce/orders/{id}</c>.
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
