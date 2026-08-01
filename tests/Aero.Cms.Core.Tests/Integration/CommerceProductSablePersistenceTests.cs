using Aero.Cms.Modules.Commerce;
using Aero.Cms.Modules.Commerce.Catalog.Models;
using AeroDB.Sable;
using Shouldly;

namespace Aero.Cms.Core.Tests.Integration;

public sealed class CommerceProductSablePersistenceTests
{
    [Test]
    public async Task Commerce_schema_allows_same_sku_in_distinct_tenants_and_persists_site_listing()
    {
        var module = new CommerceModule();
        await using var harness = new SableTestHarness().WithConfiguration(module.Configure);
        await harness.InitializeAsync();
        harness.Session.Store(new ProductDocument { Id = 91_001, TenantId = 1, Name = "Starter", Sku = "STARTER", StockQuantity = 3 });
        harness.Session.Store(new ProductDocument { Id = 91_002, TenantId = 2, Name = "Starter", Sku = "STARTER", StockQuantity = 3 });
        harness.Session.Store(new ProductListingDocument { Id = 91_003, TenantId = 1, SiteId = 11, ProductId = 91_001, Culture = "en-US", Slug = "starter", Name = "Starter", Price = 49m, IsPublished = true });
        await harness.Session.SaveChangesAsync();
        await using var verify = await harness.OpenSessionAsync();
        (await verify.Query<ProductDocument>().Where(x => x.Sku == "STARTER").ToListAsync()).Count.ShouldBe(2);
        (await verify.Query<ProductListingDocument>().FirstOrDefaultAsync(x => x.SiteId == 11 && x.Culture == "en-US"))!.ProductId.ShouldBe(91_001);
    }
}
