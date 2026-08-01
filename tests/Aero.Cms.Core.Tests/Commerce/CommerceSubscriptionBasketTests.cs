using Aero.Cms.Modules.Commerce;
using Aero.Cms.Modules.Commerce.Basket.Models;
using Aero.Cms.Modules.Commerce.Basket.Services;
using Aero.Cms.Modules.Commerce.Catalog.Models;
using Aero.Cms.Modules.Commerce.Catalog.Services;
using Aero.Cms.Modules.Commerce.Catalog.Validation;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;
using Shouldly;

namespace Aero.Cms.Core.Tests.Commerce;

public sealed class CommerceSubscriptionBasketTests
{
    [Test]
    public async Task Published_recurring_listing_requires_a_provider_binding_and_one_time_products_reject_offers()
    {
        await using var harness = await CreateHarnessAsync();
        var catalog = new ProductService(harness.Session, new ProductValidator(), new ProductListingValidator());
        var recurring = new ProductDocument
        {
            Name = "Recurring service",
            Sku = "RECUR-1",
            FulfillmentMode = ProductFulfillmentMode.NonInventoryRecurring,
            StockQuantity = 0
        };
        var oneTime = new ProductDocument { Name = "One-time service", Sku = "ONCE-1", StockQuantity = 0 };
        var createdRecurring = (await catalog.CreateProductAsync(1, recurring)).ShouldBeOfType<Result<ProductDocument, AeroError>.Ok>().Value;
        var createdOneTime = (await catalog.CreateProductAsync(1, oneTime)).ShouldBeOfType<Result<ProductDocument, AeroError>.Ok>().Value;

        var draft = (await catalog.CreateListingAsync(1, 10, Listing(createdRecurring.Id, "recurring-draft", false, null)))
            .ShouldBeOfType<Result<ProductListingDocument, AeroError>.Ok>().Value;
        (await catalog.UpdateListingAsync(1, 10, draft.Id, ListingUpdate(draft, true, new SubscriptionOffer { IntervalDays = 30 })))
            .ShouldBeOfType<Result<ProductListingDocument, AeroError>.Failure>().Error.ShouldBeOfType<AeroError.Validation>();
        (await catalog.UpdateListingAsync(1, 10, draft.Id, ListingUpdate(draft, true, Offer(30))))
            .ShouldBeOfType<Result<ProductListingDocument, AeroError>.Ok>();
        (await catalog.CreateListingAsync(1, 10, Listing(createdOneTime.Id, "one-time-offer", false, Offer(30))))
            .ShouldBeOfType<Result<ProductListingDocument, AeroError>.Failure>().Error.ShouldBeOfType<AeroError.Validation>();
    }

    [Test]
    public async Task Recurring_basket_snapshots_provider_bindings_ignores_stock_and_rejects_mixed_intents_or_intervals()
    {
        await using var harness = await CreateHarnessAsync();
        SeedProduct(harness, 100, "MONTHLY", ProductFulfillmentMode.NonInventoryRecurring, 0);
        SeedProduct(harness, 101, "ONE-TIME", ProductFulfillmentMode.Inventory, 5);
        SeedProduct(harness, 102, "BIWEEKLY", ProductFulfillmentMode.NonInventoryRecurring, 0);
        harness.Session.Store(Listing(100, "monthly", true, Offer(30), 200));
        harness.Session.Store(Listing(101, "one-time", true, null, 201));
        harness.Session.Store(Listing(102, "biweekly", true, Offer(14), 202));
        await harness.Session.SaveChangesAsync();
        var baskets = new BasketService(harness.Session);

        var basket = (await baskets.AddItemAsync(1, 10, 7, 200, 2, "en-US"))
            .ShouldBeOfType<Result<BasketDocument, AeroError>.Ok>().Value;
        var item = basket.Items.Single();
        item.BillingKind.ShouldBe(BasketBillingKind.Recurring);
        item.BillingIntervalDays.ShouldBe(30);
        item.SubscriptionOffer.ShouldBe(new BasketSubscriptionOfferSnapshot { IntervalDays = 30, StripePriceId = "price_monthly_30", PayPalPlanId = null });

        (await baskets.AddItemAsync(1, 10, 7, 200, 3, "en-US"))
            .ShouldBeOfType<Result<BasketDocument, AeroError>.Ok>().Value.Items.Single().Quantity.ShouldBe(5);
        (await baskets.AddItemAsync(1, 10, 7, 201, 1, "en-US")).IsSuccess.ShouldBeFalse();
        (await baskets.AddItemAsync(1, 10, 7, 202, 1, "en-US")).IsSuccess.ShouldBeFalse();

        await using var verify = await harness.OpenSessionAsync();
        (await verify.Query<ProductDocument>().FirstOrDefaultAsync(product => product.Id == 100))!.StockQuantity.ShouldBe(0);
    }

    [Test]
    public async Task Fulfillment_mode_update_cannot_make_existing_listings_incompatible()
    {
        await using var harness = await CreateHarnessAsync();
        var catalog = new ProductService(harness.Session, new ProductValidator(), new ProductListingValidator());
        var product = (await catalog.CreateProductAsync(1, new ProductDocument { Name = "Managed", Sku = "MANAGED", StockQuantity = 5 }))
            .ShouldBeOfType<Result<ProductDocument, AeroError>.Ok>().Value;
        var listing = (await catalog.CreateListingAsync(1, 10, Listing(product.Id, "managed", true, null)))
            .ShouldBeOfType<Result<ProductListingDocument, AeroError>.Ok>().Value;

        var currentProduct = (await catalog.GetProductAsync(1, product.Id)).ShouldBeOfType<Result<ProductDocument?, AeroError>.Ok>().Value!;
        (await catalog.UpdateProductAsync(1, product.Id, ProductUpdate(currentProduct, ProductFulfillmentMode.NonInventoryRecurring, 0)))
            .ShouldBeOfType<Result<ProductDocument, AeroError>.Failure>().Error.ShouldBeOfType<AeroError.Validation>();
        (await catalog.UpdateListingAsync(1, 10, listing.Id, ListingUpdate(listing, false))).ShouldBeOfType<Result<ProductListingDocument, AeroError>.Ok>();
        currentProduct = (await catalog.GetProductAsync(1, product.Id)).ShouldBeOfType<Result<ProductDocument?, AeroError>.Ok>().Value!;
        var transitioned = (await catalog.UpdateProductAsync(1, product.Id, ProductUpdate(currentProduct, ProductFulfillmentMode.NonInventoryRecurring, 0)))
            .ShouldBeOfType<Result<ProductDocument, AeroError>.Ok>().Value;
        transitioned.FulfillmentMode.ShouldBe(ProductFulfillmentMode.NonInventoryRecurring);
    }

    private static ProductListingDocument Listing(long productId, string slug, bool published, SubscriptionOffer? offer, long id = 0) => new()
    {
        Id = id,
        TenantId = id == 0 ? 0 : 1,
        SiteId = id == 0 ? 0 : 10,
        ProductId = productId,
        Culture = "en-US",
        Slug = slug,
        Name = slug,
        Price = 10m,
        Currency = "USD",
        IsPublished = published,
        SubscriptionOffer = offer
    };

    private static ProductDocument ProductUpdate(ProductDocument value, ProductFulfillmentMode fulfillmentMode, int stockQuantity) => new()
    {
        Name = value.Name,
        Description = value.Description,
        Sku = value.Sku,
        FulfillmentMode = fulfillmentMode,
        StockQuantity = stockQuantity,
        IsActive = value.IsActive,
        Attributes = value.Attributes,
        Tags = value.Tags,
        Version = value.Version
    };

    private static ProductListingDocument ListingUpdate(ProductListingDocument value, bool isPublished, SubscriptionOffer? subscriptionOffer = null) => new()
    {
        ProductId = value.ProductId,
        Culture = value.Culture,
        Slug = value.Slug,
        Name = value.Name,
        ShortDescription = value.ShortDescription,
        Description = value.Description,
        Category = value.Category,
        ImageUrl = value.ImageUrl,
        Price = value.Price,
        CompareAtPrice = value.CompareAtPrice,
        IsPublished = isPublished,
        IsFeatured = value.IsFeatured,
        SubscriptionOffer = subscriptionOffer,
        Version = value.Version
    };

    private static SubscriptionOffer Offer(int intervalDays) => new()
    {
        IntervalDays = intervalDays,
        StripePriceId = intervalDays == 30 ? "price_monthly_30" : "price_biweekly_14"
    };

    private static void SeedProduct(SableTestHarness harness, long id, string sku, ProductFulfillmentMode mode, int stock)
        => harness.Session.Store(new ProductDocument { Id = id, TenantId = 1, Name = sku, Sku = sku, FulfillmentMode = mode, StockQuantity = stock, IsActive = true });

    private static async Task<SableTestHarness> CreateHarnessAsync()
    {
        var harness = new SableTestHarness().WithConfiguration(new CommerceModule().Configure);
        await harness.InitializeAsync();
        return harness;
    }
}
