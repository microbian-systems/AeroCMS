using System.Net.Http.Json;

namespace Aero.Cms.Modules.Commerce.Client.Services;

/// <summary>
/// HTTP and JSON implementation of <see cref="ICommerceClientService"/>.
/// </summary>
/// <param name="http">The configured client used for all relative Commerce API routes.</param>
/// <remarks>
/// Successful create, update, and basket mutation responses are required with <c>EnsureSuccessStatusCode</c>;
/// failures therefore throw <see cref="HttpRequestException"/>. Read methods use JSON helpers directly, and
/// delete-product returns <see langword="false"/> for non-success responses instead of throwing explicitly.
/// </remarks>
public sealed class CommerceClientService(HttpClient http) : ICommerceClientService
{
    // ─── Catalog ──────────────────────────────────────────────

        /// <summary>
    /// <inheritdoc />
    /// </summary>
public async Task<IReadOnlyList<ProductDto>> GetProductsAsync(
        string? search = null, string? category = null,
        int skip = 0, int take = 20)
    {
        var query = $"?skip={skip}&take={take}";
        if (!string.IsNullOrWhiteSpace(search)) query += $"&search={Uri.EscapeDataString(search)}";
        if (!string.IsNullOrWhiteSpace(category)) query += $"&category={Uri.EscapeDataString(category)}";

        var response = await http.GetFromJsonAsync<ProductsResponse>($"/api/commerce/catalog/products{query}");
        return response?.Items ?? [];
    }

        /// <summary>
    /// <inheritdoc />
    /// </summary>
public async Task<ProductDto?> GetProductByIdAsync(long id)
        => await http.GetFromJsonAsync<ProductDto>($"/api/commerce/catalog/products/{id}");

        /// <summary>
    /// <inheritdoc />
    /// </summary>
public async Task<ProductDto?> GetProductBySlugAsync(string slug)
        => await http.GetFromJsonAsync<ProductDto>($"/api/commerce/catalog/products/by-slug/{slug}");

        /// <summary>
    /// <inheritdoc />
    /// </summary>
public async Task<ProductDto?> CreateProductAsync(CreateProductRequest request)
    {
        var response = await http.PostAsJsonAsync("/api/commerce/catalog/products", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProductDto>();
    }

        /// <summary>
    /// <inheritdoc />
    /// </summary>
public async Task<ProductDto?> UpdateProductAsync(long id, UpdateProductRequest request)
    {
        var response = await http.PutAsJsonAsync($"/api/commerce/catalog/products/{id}", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProductDto>();
    }

        /// <summary>
    /// <inheritdoc />
    /// </summary>
public async Task<bool> DeleteProductAsync(long id)
    {
        var response = await http.DeleteAsync($"/api/commerce/catalog/products/{id}");
        return response.IsSuccessStatusCode;
    }

    // ─── Basket ────────────────────────────────────────────────

        /// <summary>
    /// <inheritdoc />
    /// </summary>
public async Task<BasketDto?> GetBasketAsync(string customerId)
        => await http.GetFromJsonAsync<BasketDto>($"/api/commerce/basket?customerId={Uri.EscapeDataString(customerId)}");

        /// <summary>
    /// <inheritdoc />
    /// </summary>
public async Task<BasketDto?> AddItemToBasketAsync(string customerId, AddBasketItemRequest request)
    {
        var response = await http.PostAsJsonAsync($"/api/commerce/basket/items?customerId={Uri.EscapeDataString(customerId)}", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BasketDto>();
    }

        /// <summary>
    /// <inheritdoc />
    /// </summary>
public async Task<BasketDto?> RemoveItemFromBasketAsync(string customerId, long productId)
    {
        var response = await http.DeleteAsync($"/api/commerce/basket/items/{productId}?customerId={Uri.EscapeDataString(customerId)}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BasketDto>();
    }

        /// <summary>
    /// <inheritdoc />
    /// </summary>
public async Task<BasketDto?> ClearBasketAsync(string customerId)
    {
        var response = await http.DeleteAsync($"/api/commerce/basket?customerId={Uri.EscapeDataString(customerId)}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BasketDto>();
    }

    // ─── Orders ───────────────────────────────────────────────

        /// <summary>
    /// <inheritdoc />
    /// </summary>
public async Task<IReadOnlyList<OrderDto>> GetOrdersAsync(int skip = 0, int take = 20)
    {
        var response = await http.GetFromJsonAsync<OrdersResponse>($"/api/commerce/orders?skip={skip}&take={take}");
        return response?.Items ?? [];
    }

        /// <summary>
    /// <inheritdoc />
    /// </summary>
public async Task<OrderDto?> GetOrderByIdAsync(long id)
        => await http.GetFromJsonAsync<OrderDto>($"/api/commerce/orders/{id}");

    // ─── Internal response wrappers ───────────────────────────

    private sealed record ProductsResponse(IReadOnlyList<ProductDto> Items, long TotalCount);
    private sealed record OrdersResponse(IReadOnlyList<OrderDto> Items, long TotalCount);
}
