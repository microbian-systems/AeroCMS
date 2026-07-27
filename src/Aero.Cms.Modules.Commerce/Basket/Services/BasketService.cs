using Aero.Cms.Modules.Commerce.Basket.Models;
using Aero.Cms.Modules.Commerce.Basket.Validation;
using Aero.Cms.Modules.Commerce.Catalog.Models;
using AeroDB.Sable;
using FluentValidation;

namespace Aero.Cms.Modules.Commerce.Basket.Services;

/// <summary>Basket operations scoped by trusted tenant, site, and external-member identifiers.</summary>
public sealed class BasketService(
    IDocumentSession session,
    IValidator<BasketItem>? itemValidator = null,
    IValidator<BasketDocument>? basketValidator = null) : IBasketService
{
    private readonly IValidator<BasketItem> itemValidator = itemValidator ?? new BasketItemValidator();
    private readonly IValidator<BasketDocument> basketValidator = basketValidator ?? new BasketDocumentValidator();
    public async Task<Result<BasketDocument?, AeroError>> GetAsync(long tenantId, long siteId, long externalMemberId, CancellationToken ct = default)
    {
        try { return Prelude.Ok<BasketDocument?, AeroError>(await FindAsync(tenantId, siteId, externalMemberId, ct)); }
        catch (Exception ex) { return Prelude.Fail<BasketDocument?, AeroError>(AeroError.CreateError(ex.Message)); }
    }

    public async Task<Result<BasketDocument, AeroError>> GetOrCreateAsync(long tenantId, long siteId, long externalMemberId, CancellationToken ct = default)
    {
        try
        {
            var basket = await FindAsync(tenantId, siteId, externalMemberId, ct);
            if (basket is not null) return Prelude.Ok<BasketDocument, AeroError>(basket);
            basket = new BasketDocument { Id = Snowflake.NewId(), TenantId = tenantId, SiteId = siteId, ExternalMemberId = externalMemberId, Currency = "USD", CreatedOn = DateTimeOffset.UtcNow };
            try
            {
                session.Store(basket); await session.SaveChangesAsync(ct);
                return Prelude.Ok<BasketDocument, AeroError>(basket);
            }
            catch (Exception createFailure)
            {
                session.ClearChanges();
                var winner = await FindAsync(tenantId, siteId, externalMemberId, ct);
                return winner is not null ? Prelude.Ok<BasketDocument, AeroError>(winner) : Prelude.Fail<BasketDocument, AeroError>(AeroError.CreateError(createFailure.Message));
            }
        }
        catch (Exception ex) { return Prelude.Fail<BasketDocument, AeroError>(AeroError.CreateError(ex.Message)); }
    }

    public async Task<Result<BasketDocument, AeroError>> AddItemAsync(long tenantId, long siteId, long externalMemberId, long listingId, int quantity, string culture, CancellationToken ct = default)
    {
        if (quantity <= 0) return Prelude.Fail<BasketDocument, AeroError>(AeroError.CreateError("Quantity must be greater than zero."));
        try
        {
            var listing = await session.Query<ProductListingDocument>().FirstOrDefaultAsync(x => x.Id == listingId && x.TenantId == tenantId && x.SiteId == siteId && x.Culture == culture && x.IsPublished && x.Currency == "USD", ct);
            if (listing is null) return Prelude.Fail<BasketDocument, AeroError>(AeroError.CreateError("Listing not found."));
            var product = await session.Query<ProductDocument>().FirstOrDefaultAsync(x => x.Id == listing.ProductId && x.TenantId == tenantId && x.IsActive, ct);
            if (product is null || RequiresInventory(product) && product.StockQuantity < quantity) return Prelude.Fail<BasketDocument, AeroError>(AeroError.CreateError("Listing is unavailable."));
            if (!TryCreateSnapshot(product, listing, quantity, out var snapshot, out var failure)) return Prelude.Fail<BasketDocument, AeroError>(AeroError.CreateError(failure));
            var basketResult = await GetOrCreateAsync(tenantId, siteId, externalMemberId, ct);
            if (basketResult is not Result<BasketDocument, AeroError>.Ok(var basket)) return basketResult;
            var existing = basket.Items.FirstOrDefault(x => x.ListingId == listingId);
            var finalQuantity = quantity + (existing?.Quantity ?? 0);
            if (RequiresInventory(product) && finalQuantity > product.StockQuantity) return Prelude.Fail<BasketDocument, AeroError>(AeroError.CreateError("Insufficient stock."));
            if (!TryCreateSnapshot(product, listing, finalQuantity, out snapshot, out failure)) return Prelude.Fail<BasketDocument, AeroError>(AeroError.CreateError(failure));
            if (!IsCompatibleWithBasket(basket, listingId, snapshot)) return Prelude.Fail<BasketDocument, AeroError>(AeroError.CreateError("A basket cannot mix one-time and recurring items or recurring intervals."));
            if (existing is null) basket.Items.Add(snapshot); else basket.Items[basket.Items.IndexOf(existing)] = snapshot;
            var basketValidation = await ValidateBasketAsync(basket, ct);
            if (!basketValidation.IsValid) return Prelude.Fail<BasketDocument, AeroError>(AeroError.CreateError(basketValidation.Failure));
            basket.ModifiedOn = DateTimeOffset.UtcNow; session.Store(basket); await session.SaveChangesAsync(ct);
            return Prelude.Ok<BasketDocument, AeroError>(basket);
        }
        catch (Exception ex) { return Prelude.Fail<BasketDocument, AeroError>(AeroError.CreateError(ex.Message)); }
    }

    public async Task<Result<BasketDocument, AeroError>> UpdateQuantityAsync(long tenantId, long siteId, long externalMemberId, long listingId, int quantity, string culture, CancellationToken ct = default)
    {
        try
        {
            var basket = await FindAsync(tenantId, siteId, externalMemberId, ct);
            if (basket is null) return Prelude.Fail<BasketDocument, AeroError>(AeroError.CreateError("Basket not found."));
            var item = basket.Items.FirstOrDefault(x => x.ListingId == listingId);
            if (item is null) return Prelude.Fail<BasketDocument, AeroError>(AeroError.CreateError("Basket item not found."));
            if (quantity <= 0) basket.Items.Remove(item);
            else
            {
                var listing = await session.Query<ProductListingDocument>().FirstOrDefaultAsync(x => x.Id == listingId && x.TenantId == tenantId && x.SiteId == siteId && x.Culture == culture && x.IsPublished && x.Currency == "USD", ct);
                if (listing is null) return Prelude.Fail<BasketDocument, AeroError>(AeroError.CreateError("Basket item not found."));
                var product = await session.Query<ProductDocument>().FirstOrDefaultAsync(x => x.Id == listing.ProductId && x.TenantId == tenantId && x.IsActive, ct);
                if (product is null || RequiresInventory(product) && product.StockQuantity < quantity) return Prelude.Fail<BasketDocument, AeroError>(AeroError.CreateError("Insufficient stock."));
                if (!TryCreateSnapshot(product, listing, quantity, out var snapshot, out var failure)) return Prelude.Fail<BasketDocument, AeroError>(AeroError.CreateError(failure));
                if (!IsCompatibleWithBasket(basket, listingId, snapshot)) return Prelude.Fail<BasketDocument, AeroError>(AeroError.CreateError("A basket cannot mix one-time and recurring items or recurring intervals."));
                basket.Items[basket.Items.IndexOf(item)] = snapshot;
            }
            var basketValidation = await ValidateBasketAsync(basket, ct);
            if (!basketValidation.IsValid) return Prelude.Fail<BasketDocument, AeroError>(AeroError.CreateError(basketValidation.Failure));
            basket.ModifiedOn = DateTimeOffset.UtcNow; session.Store(basket); await session.SaveChangesAsync(ct);
            return Prelude.Ok<BasketDocument, AeroError>(basket);
        }
        catch (Exception ex) { return Prelude.Fail<BasketDocument, AeroError>(AeroError.CreateError(ex.Message)); }
    }

    public async Task<Result<BasketDocument, AeroError>> ClearAsync(long tenantId, long siteId, long externalMemberId, CancellationToken ct = default)
    {
        try
        {
            var basket = await FindAsync(tenantId, siteId, externalMemberId, ct);
            if (basket is null) return Prelude.Fail<BasketDocument, AeroError>(AeroError.CreateError("Basket not found."));
            basket.Items.Clear(); basket.ModifiedOn = DateTimeOffset.UtcNow; session.Store(basket); await session.SaveChangesAsync(ct);
            return Prelude.Ok<BasketDocument, AeroError>(basket);
        }
        catch (Exception ex) { return Prelude.Fail<BasketDocument, AeroError>(AeroError.CreateError(ex.Message)); }
    }

    private Task<BasketDocument?> FindAsync(long tenantId, long siteId, long externalMemberId, CancellationToken ct) => session.Query<BasketDocument>().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.SiteId == siteId && x.ExternalMemberId == externalMemberId, ct);

    private static bool RequiresInventory(ProductDocument product) => product.FulfillmentMode == ProductFulfillmentMode.Inventory;

    private bool TryCreateSnapshot(ProductDocument product, ProductListingDocument listing, int quantity, out BasketItem snapshot, out string failure)
    {
        failure = string.Empty;
        snapshot = new BasketItem
        {
            ListingId = listing.Id,
            ProductId = product.Id,
            ProductName = listing.Name,
            Sku = product.Sku,
            ImageUrl = listing.ImageUrl,
            Quantity = quantity,
            UnitPrice = listing.Price,
            Currency = "USD"
        };

        switch (product.FulfillmentMode)
        {
            case ProductFulfillmentMode.Inventory:
            case ProductFulfillmentMode.NonInventoryOneTime:
                if (listing.SubscriptionOffer is not null)
                {
                    failure = "Listing is unavailable.";
                    return false;
                }
                snapshot = snapshot with { BillingKind = BasketBillingKind.OneTime };
                break;
            case ProductFulfillmentMode.NonInventoryRecurring when IsValidOffer(listing.SubscriptionOffer):
                var offer = listing.SubscriptionOffer!;
                snapshot = snapshot with
                {
                    BillingKind = BasketBillingKind.Recurring,
                    BillingIntervalDays = offer.IntervalDays,
                    SubscriptionOffer = new BasketSubscriptionOfferSnapshot
                    {
                        IntervalDays = offer.IntervalDays,
                        StripePriceId = offer.StripePriceId,
                        PayPalPlanId = offer.PayPalPlanId
                    }
                };
                break;
            default:
                failure = "Listing is unavailable.";
                return false;
        }

        var validation = itemValidator.Validate(snapshot);
        if (!validation.IsValid)
        {
            failure = validation.Errors.First().ErrorMessage;
            return false;
        }

        return true;
    }

    private static bool IsValidOffer(SubscriptionOffer? offer)
        => offer is { IntervalDays: >= 1 and <= 365 } &&
           (!string.IsNullOrWhiteSpace(offer.StripePriceId) || !string.IsNullOrWhiteSpace(offer.PayPalPlanId));

    private static bool IsCompatibleWithBasket(BasketDocument basket, long replacingListingId, BasketItem candidate)
        => basket.Items.Where(item => item.ListingId != replacingListingId).All(item =>
            item.BillingKind == candidate.BillingKind &&
            (candidate.BillingKind != BasketBillingKind.Recurring || item.BillingIntervalDays == candidate.BillingIntervalDays));

    private async Task<(bool IsValid, string Failure)> ValidateBasketAsync(BasketDocument basket, CancellationToken ct)
    {
        var validation = await basketValidator.ValidateAsync(basket, ct);
        return (validation.IsValid, validation.IsValid ? string.Empty : validation.Errors.First().ErrorMessage);
    }
}
