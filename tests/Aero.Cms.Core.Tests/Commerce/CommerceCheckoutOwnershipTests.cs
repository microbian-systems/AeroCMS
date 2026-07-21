using Aero.Cms.Modules.Commerce;
using Aero.Cms.Modules.Commerce.Basket.Models;
using Aero.Cms.Modules.Commerce.Basket.Services;
using Aero.Cms.Modules.Commerce.Catalog.Models;
using Aero.Cms.Modules.Commerce.Catalog.Services;
using Aero.Cms.Modules.Commerce.Catalog.Validation;
using Aero.Cms.Modules.Commerce.Orders.Domain;
using Aero.Cms.Modules.Commerce.Orders.Services;
using Aero.Core;
using Aero.Core.Railway;
using Shouldly;

namespace Aero.Cms.Core.Tests.Commerce;

public sealed class CommerceCheckoutOwnershipTests
{
    [Test]
    public async Task Checkout_reprices_listing_decrements_tenant_stock_and_clears_only_owned_basket()
    {
        await using var harness = await CreateHarnessAsync();
        harness.Session.Store(new ProductDocument { Id = 100, TenantId = 1, Name = "Canonical", Sku = "SKU-100", StockQuantity = 5, IsActive = true });
        harness.Session.Store(new ProductListingDocument { Id = 200, TenantId = 1, SiteId = 10, ProductId = 100, Culture = "en-US", Slug = "canonical", Name = "Display", Price = 27m, IsPublished = true });
        harness.Session.Store(new BasketDocument { Id = 300, TenantId = 1, SiteId = 10, ExternalMemberId = 7, Items = [new BasketItem { ListingId = 200, ProductId = 100, ProductName = "Forged", Sku = "FORGED", Quantity = 2, UnitPrice = 1m }] });
        harness.Session.Store(new BasketDocument { Id = 301, TenantId = 1, SiteId = 11, ExternalMemberId = 7, Items = [new BasketItem { ListingId = 200, ProductId = 100, ProductName = "Other", Sku = "OTHER", Quantity = 1, UnitPrice = 1m }] });
        await harness.Session.SaveChangesAsync();
        var service = new OrderService(harness.Session);
        var result = await service.CheckoutAsync(1, 10, 7, Address(), null, "en-US");
        var order = result.ShouldBeOfType<Result<OrderEntity, AeroError>.Ok>().Value;
        order.Items.Single().UnitPrice.ShouldBe(27m);
        order.Items.Single().Sku.ShouldBe("SKU-100");
        await using var verify = await harness.OpenSessionAsync();
        (await verify.Query<ProductDocument>().FirstOrDefaultAsync(x => x.Id == 100))!.StockQuantity.ShouldBe(3);
        (await verify.Query<BasketDocument>().FirstOrDefaultAsync(x => x.Id == 300))!.Items.ShouldBeEmpty();
        (await verify.Query<BasketDocument>().FirstOrDefaultAsync(x => x.Id == 301))!.Items.Count.ShouldBe(1);
    }

    [Test]
    public async Task Cancellation_is_member_and_site_scoped_and_releases_stock_only_from_allowed_status()
    {
        await using var harness = await CreateHarnessAsync();
        harness.Session.Store(new ProductDocument { Id = 101, TenantId = 1, Name = "Canonical", Sku = "SKU-101", StockQuantity = 1, IsActive = true });
        harness.Session.Store(new OrderEntity { Id = 401, TenantId = 1, SiteId = 10, ExternalMemberId = 7, Status = OrderStatus.Submitted, Items = [new OrderItem { ProductId = 101, ListingId = 201, ProductName = "Display", Sku = "SKU-101", Quantity = 2, UnitPrice = 10m }] });
        await harness.Session.SaveChangesAsync();
        var service = new OrderService(harness.Session);
        (await service.CancelAsync(1, 10, 8, 401)).IsSuccess.ShouldBeFalse();
        await using var verifyAfterDenied = await harness.OpenSessionAsync();
        (await verifyAfterDenied.Query<ProductDocument>().FirstOrDefaultAsync(x => x.Id == 101))!.StockQuantity.ShouldBe(1);
        var cancelled = await service.CancelAsync(1, 10, 7, 401);
        cancelled.ShouldBeOfType<Result<OrderEntity, AeroError>.Ok>().Value.Status.ShouldBe(OrderStatus.Cancelled);
        await using var verifyAfterCancel = await harness.OpenSessionAsync();
        (await verifyAfterCancel.Query<ProductDocument>().FirstOrDefaultAsync(x => x.Id == 101))!.StockQuantity.ShouldBe(3);
    }

    [Test]
    public async Task Quantity_update_reloads_authoritative_listing_snapshot_and_rejects_wrong_site_or_stock()
    {
        await using var harness = await CreateHarnessAsync();
        harness.Session.Store(new ProductDocument { Id = 102, TenantId = 1, Name = "Canonical", Sku = "SKU-102", StockQuantity = 3, IsActive = true });
        harness.Session.Store(new ProductListingDocument { Id = 202, TenantId = 1, SiteId = 10, ProductId = 102, Culture = "en-US", Slug = "display", Name = "New display", ImageUrl = "/new.jpg", Price = 39m, IsPublished = true });
        harness.Session.Store(new BasketDocument { Id = 302, TenantId = 1, SiteId = 10, ExternalMemberId = 7, Items = [new BasketItem { ListingId = 202, ProductId = 102, ProductName = "Old", Sku = "OLD", Quantity = 1, UnitPrice = 1m }] });
        await harness.Session.SaveChangesAsync();
        var baskets = new BasketService(harness.Session);
        var updated = await baskets.UpdateQuantityAsync(1, 10, 7, 202, 2, "en-US");
        var item = updated.ShouldBeOfType<Result<BasketDocument, AeroError>.Ok>().Value.Items.Single();
        item.ProductName.ShouldBe("New display"); item.Sku.ShouldBe("SKU-102"); item.UnitPrice.ShouldBe(39m);
        (await baskets.UpdateQuantityAsync(1, 11, 7, 202, 2, "en-US")).IsSuccess.ShouldBeFalse();
        (await baskets.UpdateQuantityAsync(1, 10, 7, 202, 4, "en-US")).IsSuccess.ShouldBeFalse();
    }

    [Test]
    public async Task Catalog_service_rejects_negative_stock_and_invalid_listing_price()
    {
        await using var harness = await CreateHarnessAsync();
        var catalog = new ProductService(harness.Session, new ProductValidator(), new ProductListingValidator());
        (await catalog.CreateProductAsync(1, new ProductDocument { Name = "Bad", Sku = "BAD", StockQuantity = -1 })).IsSuccess.ShouldBeFalse();
        harness.Session.Store(new ProductDocument { Id = 103, TenantId = 1, Name = "Good", Sku = "GOOD", StockQuantity = 1 });
        await harness.Session.SaveChangesAsync();
        (await catalog.CreateListingAsync(1, 10, new ProductListingDocument { ProductId = 103, Culture = "en-US", Slug = "bad", Name = "Bad", Price = -1m })).IsSuccess.ShouldBeFalse();
    }

    [Test]
    public async Task Basket_get_is_non_mutating_when_no_owned_basket_exists()
    {
        await using var harness = await CreateHarnessAsync();
        var baskets = new BasketService(harness.Session);
        var result = await baskets.GetAsync(1, 10, 7);
        result.ShouldBeOfType<Result<BasketDocument?, AeroError>.Ok>().Value.ShouldBeNull();
        (await harness.Session.Query<BasketDocument>().ToListAsync()).ShouldBeEmpty();
    }

    private static async Task<SableTestHarness> CreateHarnessAsync()
    {
        var harness = new SableTestHarness().WithConfiguration(new CommerceModule().Configure);
        await harness.InitializeAsync();
        return harness;
    }
    private static Address Address() => new() { Street = "1 Main", City = "Austin", PostalCode = "78701", Country = "US" };
}
