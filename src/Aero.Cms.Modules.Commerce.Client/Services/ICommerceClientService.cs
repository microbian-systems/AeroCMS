namespace Aero.Cms.Modules.Commerce.Client.Services;

/// <summary>Typed Commerce client whose customer calls never select ownership or commercial snapshots.</summary>
public interface ICommerceClientService
{
    Task<IReadOnlyList<ListingDto>> GetListingsAsync(string? search = null, string? category = null, int skip = 0, int take = 20);
    Task<ListingDto?> GetListingBySlugAsync(string slug);
    Task<BasketDto?> GetBasketAsync();
    Task<BasketDto?> AddItemToBasketAsync(AddBasketItemRequest request);
    Task<BasketDto?> UpdateBasketQuantityAsync(long listingId, UpdateBasketQuantityRequest request);
    Task<BasketDto?> RemoveItemFromBasketAsync(long listingId);
    Task<IReadOnlyList<OrderDto>> GetOrdersAsync(int skip = 0, int take = 20);
    Task<OrderDto?> GetOrderByIdAsync(long id);
    Task<OrderDto?> CheckoutAsync(CheckoutRequest request);
}

public sealed record ListingDto(long Id, string Slug, string Name, string? ShortDescription, string? Description, string? Category, string? ImageUrl, decimal Price, decimal? CompareAtPrice, string Currency, bool IsFeatured);
public sealed record AddBasketItemRequest(long ListingId, int Quantity);
public sealed record UpdateBasketQuantityRequest(int Quantity);
public sealed record BasketDto(long Id, decimal TotalAmount, string Currency, List<BasketItemDto> Items);
public sealed record BasketItemDto(long ListingId, long ProductId, string ProductName, string Sku, string? ImageUrl, int Quantity, decimal UnitPrice, string Currency, decimal TotalPrice);
public sealed record CheckoutRequest(AddressDto ShippingAddress, AddressDto? BillingAddress = null);
public sealed record AddressDto(string Street, string City, string? State, string PostalCode, string Country);
public sealed record OrderDto(long Id, string Status, decimal TotalAmount, string Currency, DateTimeOffset CreatedOn, List<OrderItemDto> Items);
public sealed record OrderItemDto(long ListingId, long ProductId, string ProductName, string Sku, int Quantity, decimal UnitPrice, string Currency);
