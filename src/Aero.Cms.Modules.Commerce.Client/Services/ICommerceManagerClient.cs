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
    Task<Result<ManagerSubscriptionPage<ManagerSubscriptionSummaryDto>, AeroError>> GetSubscriptionsAsync(int skip, int take, CancellationToken ct = default);
    Task<Result<ManagerSubscriptionReceiptDto, AeroError>> GetSubscriptionAsync(long id, CancellationToken ct = default);
}

public sealed record ManagerCatalogPage<T>(IReadOnlyList<T> Items, long TotalCount);
public sealed record ManagerSubscriptionPage<T>(IReadOnlyList<T> Items, long TotalCount);
public enum ManagerProductFulfillmentMode
{
    Inventory = 0,
    NonInventoryOneTime = 1,
    NonInventoryRecurring = 2
}

/// <summary>Safe provider-plan bindings that a manager can configure for a recurring listing.</summary>
public sealed record ManagerSubscriptionOffer(int IntervalDays, string? StripePriceId, string? PayPalPlanId);

public sealed record ManagerProductDto(long Id, string Name, string? Description, string Sku, int StockQuantity, bool IsActive, IReadOnlyDictionary<string, string> Attributes, IReadOnlyList<string> Tags, long Version, ManagerProductFulfillmentMode FulfillmentMode = ManagerProductFulfillmentMode.Inventory);
public sealed record ManagerListingDto(long Id, long ProductId, string Culture, string Slug, string Name, string? ShortDescription, string? Description, string? Category, string? ImageUrl, decimal Price, decimal? CompareAtPrice, string Currency, bool IsPublished, bool IsFeatured, long Version, ManagerSubscriptionOffer? SubscriptionOffer = null);
public sealed record ManagerProductRequest(string Name, string? Description, string Sku, int StockQuantity, bool IsActive, Dictionary<string, string> Attributes, List<string> Tags, long Version, ManagerProductFulfillmentMode FulfillmentMode = ManagerProductFulfillmentMode.Inventory);
public sealed record ManagerListingRequest(long ProductId, string Culture, string Slug, string Name, string? ShortDescription, string? Description, string? Category, string? ImageUrl, decimal Price, decimal? CompareAtPrice, bool IsPublished, bool IsFeatured, long Version, ManagerSubscriptionOffer? SubscriptionOffer = null);
public enum ManagerSubscriptionState { PendingProviderConfirmation = 0, Active = 10, PastDue = 20, Cancelled = 30, Expired = 40, ManualReview = 100 }
public enum ManagerSubscriptionCycleState { Open = 0, Paid = 10, Failed = 20, Cancelled = 30, ManualReview = 100 }
public sealed record ManagerSubscriptionSummaryDto(long SubscriptionId, long OrderId, string Provider, ManagerSubscriptionState State, int IntervalDays, decimal Amount, string Currency, DateTimeOffset CreatedOn, DateTimeOffset? CurrentPeriodEndsOn, bool CancelAtPeriodEnd, bool RequiresManualReview);
public sealed record ManagerSubscriptionCycleDto(int CycleNumber, decimal Amount, string Currency, DateTimeOffset PeriodStartsOn, DateTimeOffset PeriodEndsOn, ManagerSubscriptionCycleState State, bool RequiresManualReview);
public sealed record ManagerSubscriptionLineDto(string ProductName, string ListingName, string Sku, int Quantity, decimal UnitAmount, decimal TotalAmount);
public sealed record ManagerSubscriptionReceiptDto(ManagerSubscriptionSummaryDto Subscription, IReadOnlyList<ManagerSubscriptionLineDto> Lines, IReadOnlyList<ManagerSubscriptionCycleDto> Cycles);
