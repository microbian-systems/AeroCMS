using System.Net.Http.Json;

namespace Aero.Cms.Modules.Commerce.Client.Services;

/// <summary>HTTP implementation for the customer-scoped Commerce endpoints.</summary>
public sealed class CommerceClientService(HttpClient http) : ICommerceClientService
{
    public async Task<IReadOnlyList<ListingDto>> GetListingsAsync(string? search = null, string? category = null, int skip = 0, int take = 20)
    { var url = $"/api/commerce/catalog/listings?skip={skip}&take={take}"; if (!string.IsNullOrWhiteSpace(search)) url += $"&search={Uri.EscapeDataString(search)}"; if (!string.IsNullOrWhiteSpace(category)) url += $"&category={Uri.EscapeDataString(category)}"; return (await http.GetFromJsonAsync<ListingPage>(url))?.Items ?? []; }
    public Task<ListingDto?> GetListingBySlugAsync(string slug) => http.GetFromJsonAsync<ListingDto>($"/api/commerce/catalog/listings/by-slug/{Uri.EscapeDataString(slug)}");
    public Task<BasketDto?> GetBasketAsync() => http.GetFromJsonAsync<BasketDto>("/api/commerce/basket/");
    public async Task<BasketDto?> AddItemToBasketAsync(AddBasketItemRequest request) { var response = await http.PostAsJsonAsync("/api/commerce/basket/items", request); response.EnsureSuccessStatusCode(); return await response.Content.ReadFromJsonAsync<BasketDto>(); }
    public async Task<BasketDto?> UpdateBasketQuantityAsync(long listingId, UpdateBasketQuantityRequest request) { var response = await http.PutAsJsonAsync($"/api/commerce/basket/items/{listingId}", request); response.EnsureSuccessStatusCode(); return await response.Content.ReadFromJsonAsync<BasketDto>(); }
    public async Task<BasketDto?> RemoveItemFromBasketAsync(long listingId) { var response = await http.DeleteAsync($"/api/commerce/basket/items/{listingId}"); response.EnsureSuccessStatusCode(); return await response.Content.ReadFromJsonAsync<BasketDto>(); }
    public async Task<IReadOnlyList<OrderDto>> GetOrdersAsync(int skip = 0, int take = 20) => (await http.GetFromJsonAsync<OrderPage>($"/api/commerce/orders/?skip={skip}&take={take}"))?.Items ?? [];
    public Task<OrderDto?> GetOrderByIdAsync(long id) => http.GetFromJsonAsync<OrderDto>($"/api/commerce/orders/{id}");
    public async Task<OrderDto?> CheckoutAsync(CheckoutRequest request) { var response = await http.PostAsJsonAsync("/api/commerce/orders/", request); response.EnsureSuccessStatusCode(); return await response.Content.ReadFromJsonAsync<OrderDto>(); }
    private sealed record ListingPage(IReadOnlyList<ListingDto> Items, long TotalCount);
    private sealed record OrderPage(IReadOnlyList<OrderDto> Items, long TotalCount);
}
