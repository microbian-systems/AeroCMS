using System.Net;
using System.Net.Http.Json;
using Aero.Cms.Core.Entities;
using Aero.Cms.Core.Tests.Integration;
using Aero.Cms.Modules.Commerce;
using Aero.Cms.Modules.Commerce.Catalog.Api;
using Aero.Cms.Modules.Commerce.Catalog.Models;
using Aero.Cms.Modules.Commerce.Catalog.Services;
using Aero.Cms.Modules.Commerce.Catalog.Validation;
using Aero.Cms.Modules.Sites;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using AeroDB.Sable;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using SableSessionOptions = AeroDB.Sable.SessionOptions;

namespace Aero.Cms.Core.Tests.Commerce;

public sealed class CommerceCatalogManagerTests
{
    private const long SiteId = 10;
    private const long TenantId = 42;
    private const long UserId = 77;

    [Test]
    public async Task Manager_routes_use_canonical_admin_prefix_and_exact_site_policies()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(Substitute.For<IProductService>());
        builder.Services.AddSingleton(Substitute.For<ICommerceManagerScopeResolver>());
        builder.Services.AddSingleton(Substitute.For<ISiteContext>());
        await using var app = builder.Build();
        app.MapCatalogApi();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(x => x.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();
        var managerEndpoints = endpoints
            .Where(x => x.RoutePattern.RawText?.StartsWith("/api/v1/admin/commerce/catalog", StringComparison.Ordinal) == true)
            .ToList();

        managerEndpoints.Count.ShouldBe(10);
        endpoints.Any(x => (x.RoutePattern.RawText ?? string.Empty).StartsWith("/api/commerce/catalog/manager", StringComparison.Ordinal))
            .ShouldBeFalse();

        var expected = new (string Method, string Route, string Policy)[]
        {
            ("GET", "/api/v1/admin/commerce/catalog/products", "site:read"),
            ("GET", "/api/v1/admin/commerce/catalog/products/{id:long}", "site:read"),
            ("POST", "/api/v1/admin/commerce/catalog/products", "site:create"),
            ("PUT", "/api/v1/admin/commerce/catalog/products/{id:long}", "site:update"),
            ("DELETE", "/api/v1/admin/commerce/catalog/products/{id:long}", "site:delete"),
            ("GET", "/api/v1/admin/commerce/catalog/listings", "site:read"),
            ("GET", "/api/v1/admin/commerce/catalog/listings/{id:long}", "site:read"),
            ("POST", "/api/v1/admin/commerce/catalog/listings", "site:create"),
            ("PUT", "/api/v1/admin/commerce/catalog/listings/{id:long}", "site:update"),
            ("DELETE", "/api/v1/admin/commerce/catalog/listings/{id:long}", "site:delete")
        };

        foreach (var item in expected)
        {
            var endpoint = managerEndpoints.Single(x =>
                x.RoutePattern.RawText == item.Route &&
                x.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods.Contains(item.Method));
            var policies = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(x => x.Policy).ToList();
            policies.ShouldBe([item.Policy]);
        }
    }

    [Test]
    public async Task Manager_scope_derives_tenant_from_persisted_selected_site()
    {
        await using var harness = await CreateHarnessAsync();
        harness.Session.Store(new SitesModel { Id = 10, TenantId = 42, Name = "Store" });
        await harness.Session.SaveChangesAsync();
        var untrustedContext = Substitute.For<ISiteContext>();
        untrustedContext.SiteId.Returns(10);
        untrustedContext.TenantId.Returns(999);

        var accessor = CreateHttpContextAccessor(10);
        await using var freshSession = await harness.OpenSessionAsync();
        var result = await new CommerceManagerScopeResolver(untrustedContext, accessor, freshSession).ResolveAsync();

        result.ShouldBeOfType<Result<CommerceManagerScope, AeroError>.Ok>().Value
            .ShouldBe(new CommerceManagerScope(42, 10));

        untrustedContext.SiteId.Returns(11);
        var missing = await new CommerceManagerScopeResolver(untrustedContext, accessor, freshSession).ResolveAsync();
        missing.ShouldBeOfType<Result<CommerceManagerScope, AeroError>.Failure>().Error
            .ShouldBeOfType<AeroError.NotFound>();
    }

    [Test]
    public async Task Assigned_selected_site_authorizes_and_uses_the_same_persisted_scope()
    {
        await using var harness = await CreateHttpHarnessAsync(["read"]);
        var service = Substitute.For<IProductService>();
        service.SearchProductsAsync(TenantId, null, 0, 20, Arg.Any<CancellationToken>())
            .Returns(Prelude.Ok<(IReadOnlyList<ProductDocument>, long), AeroError>(([], 0)));
        await using var app = await CreateHttpAppAsync(harness.Session, service);
        using var request = ManagerRequest(HttpMethod.Get, "/api/v1/admin/commerce/catalog/products", includeCookie: true);

        using var response = await app.GetTestClient().SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await service.Received(1).SearchProductsAsync(TenantId, null, 0, 20, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Forged_unassigned_site_is_forbidden_before_catalog_service()
    {
        await using var harness = await CreateHttpHarnessAsync([]);
        var service = Substitute.For<IProductService>();
        await using var app = await CreateHttpAppAsync(harness.Session, service);
        using var request = ManagerRequest(HttpMethod.Get, "/api/v1/admin/commerce/catalog/products", includeCookie: true);

        using var response = await app.GetTestClient().SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        await service.DidNotReceiveWithAnyArgs().SearchProductsAsync(default, default, default, default, default);
    }

    [Test]
    public async Task Admin_without_explicit_selected_site_cookie_fails_closed_before_catalog_service()
    {
        await using var harness = await CreateHttpHarnessAsync([]);
        var service = Substitute.For<IProductService>();
        await using var app = await CreateHttpAppAsync(harness.Session, service);
        using var request = ManagerRequest(HttpMethod.Get, "/api/v1/admin/commerce/catalog/products", includeCookie: false, isAdmin: true);

        using var response = await app.GetTestClient().SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await service.DidNotReceiveWithAnyArgs().SearchProductsAsync(default, default, default, default, default);
    }

    [Test]
    public async Task Request_json_cannot_override_product_or_listing_ownership()
    {
        await using var harness = await CreateHttpHarnessAsync(["create", "update"]);
        var service = Substitute.For<IProductService>();
        long capturedProductTenant = -1;
        (long TenantId, long SiteId, long DocumentTenantId, long DocumentSiteId, long Version) capturedListing = default;
        service.CreateProductAsync(TenantId, Arg.Any<ProductDocument>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var product = call.ArgAt<ProductDocument>(1);
                capturedProductTenant = product.TenantId;
                product.Id = 501;
                product.TenantId = TenantId;
                product.Version = 1;
                return Prelude.Ok<ProductDocument, AeroError>(product);
            });
        service.UpdateListingAsync(TenantId, SiteId, 601, Arg.Any<ProductListingDocument>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var listing = call.ArgAt<ProductListingDocument>(3);
                capturedListing = (call.ArgAt<long>(0), call.ArgAt<long>(1), listing.TenantId, listing.SiteId, listing.Version);
                listing.Id = 601;
                listing.TenantId = TenantId;
                listing.SiteId = SiteId;
                listing.Version = 8;
                return Prelude.Ok<ProductListingDocument, AeroError>(listing);
            });
        await using var app = await CreateHttpAppAsync(harness.Session, service);

        using var create = ManagerRequest(HttpMethod.Post, "/api/v1/admin/commerce/catalog/products", true);
        create.Content = JsonContent.Create(new
        {
            tenantId = 999L,
            siteId = 999L,
            name = "Server scoped",
            description = "Product",
            sku = "scope-1",
            stockQuantity = 2,
            isActive = true,
            attributes = new Dictionary<string, string>(),
            tags = Array.Empty<string>(),
            version = 99L
        });
        using var createResponse = await app.GetTestClient().SendAsync(create);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await createResponse.Content.ReadAsStringAsync()).ShouldNotContain("tenantId", Case.Insensitive);
        capturedProductTenant.ShouldBe(0);
        await service.Received(1).CreateProductAsync(TenantId, Arg.Any<ProductDocument>(), Arg.Any<CancellationToken>());

        using var update = ManagerRequest(HttpMethod.Put, "/api/v1/admin/commerce/catalog/listings/601", true);
        update.Content = JsonContent.Create(new
        {
            tenantId = 999L,
            siteId = 999L,
            productId = 501L,
            culture = "en-us",
            slug = "server-scoped",
            name = "Server scoped listing",
            shortDescription = (string?)null,
            description = (string?)null,
            category = (string?)null,
            imageUrl = (string?)null,
            price = 10m,
            compareAtPrice = (decimal?)null,
            isPublished = true,
            isFeatured = false,
            version = 7L
        });
        using var updateResponse = await app.GetTestClient().SendAsync(update);
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await updateResponse.Content.ReadAsStringAsync()).ShouldNotContain("siteId", Case.Insensitive);
        capturedListing.ShouldBe((TenantId, SiteId, 0, 0, 7));
        await service.Received(1).UpdateListingAsync(TenantId, SiteId, 601, Arg.Any<ProductListingDocument>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Manager_collections_and_by_id_reads_conceal_other_tenants_and_sites()
    {
        await using var harness = await CreateHarnessAsync();
        harness.Session.Store(new ProductDocument { Id = 100, TenantId = 1, Name = "Alpha", Sku = "A", StockQuantity = 3, IsActive = true });
        harness.Session.Store(new ProductDocument { Id = 101, TenantId = 1, Name = "Beta", Sku = "B", StockQuantity = 4, IsActive = true });
        harness.Session.Store(new ProductDocument { Id = 200, TenantId = 2, Name = "Foreign", Sku = "F", StockQuantity = 5, IsActive = true });
        harness.Session.Store(new ProductListingDocument { Id = 300, TenantId = 1, SiteId = 10, ProductId = 100, Culture = "en-US", Slug = "alpha", Name = "Alpha listing", Price = 10m });
        harness.Session.Store(new ProductListingDocument { Id = 301, TenantId = 1, SiteId = 11, ProductId = 101, Culture = "en-US", Slug = "beta", Name = "Beta listing", Price = 11m });
        harness.Session.Store(new ProductListingDocument { Id = 302, TenantId = 2, SiteId = 20, ProductId = 200, Culture = "en-US", Slug = "foreign", Name = "Foreign listing", Price = 12m });
        await harness.Session.SaveChangesAsync();
        var service = CreateService(harness);

        var products = (await service.SearchProductsAsync(1, skip: 0, take: 1))
            .ShouldBeOfType<Result<(IReadOnlyList<ProductDocument> Items, long TotalCount), AeroError>.Ok>().Value;
        products.TotalCount.ShouldBe(2);
        products.Items.Single().Id.ShouldBe(100);
        (await service.GetProductAsync(1, 200))
            .ShouldBeOfType<Result<ProductDocument?, AeroError>.Ok>().Value.ShouldBeNull();

        var listings = (await service.SearchListingsAsync(1, 10))
            .ShouldBeOfType<Result<(IReadOnlyList<ProductListingDocument> Items, long TotalCount), AeroError>.Ok>().Value;
        listings.TotalCount.ShouldBe(1);
        listings.Items.Single().Id.ShouldBe(300);
        (await service.GetListingAsync(1, 10, 301))
            .ShouldBeOfType<Result<ProductListingDocument?, AeroError>.Ok>().Value.ShouldBeNull();
    }

    [Test]
    public async Task Product_delete_conflicts_while_any_tenant_listing_references_it()
    {
        await using var harness = await CreateHarnessAsync();
        harness.Session.Store(new ProductDocument { Id = 100, TenantId = 1, Name = "Listed", Sku = "LISTED", StockQuantity = 3, IsActive = true });
        harness.Session.Store(new ProductDocument { Id = 101, TenantId = 1, Name = "Unlisted", Sku = "UNLISTED", StockQuantity = 3, IsActive = true });
        harness.Session.Store(new ProductListingDocument { Id = 300, TenantId = 1, SiteId = 10, ProductId = 100, Culture = "en-US", Slug = "listed", Name = "Listed", Price = 10m });
        await harness.Session.SaveChangesAsync();
        var service = CreateService(harness);

        var blocked = await service.DeleteProductAsync(1, 100);
        blocked.ShouldBeOfType<Result<bool, AeroError>.Failure>().Error.ShouldBeOfType<AeroError.Conflict>();
        (await service.GetProductAsync(1, 100)).ShouldBeOfType<Result<ProductDocument?, AeroError>.Ok>().Value.ShouldNotBeNull();

        (await service.DeleteProductAsync(1, 101)).ShouldBeOfType<Result<bool, AeroError>.Ok>().Value.ShouldBeTrue();
        (await service.GetProductAsync(1, 101)).ShouldBeOfType<Result<ProductDocument?, AeroError>.Ok>().Value.ShouldBeNull();
    }

    [Test]
    public async Task Stale_product_and_listing_updates_return_conflict_without_mutation()
    {
        await using var harness = await CreateHarnessAsync();
        harness.Session.Store(new ProductDocument { Id = 100, TenantId = 1, Name = "Current", Sku = "CURRENT", StockQuantity = 3, IsActive = true });
        harness.Session.Store(new ProductListingDocument { Id = 300, TenantId = 1, SiteId = 10, ProductId = 100, Culture = "en-US", Slug = "current", Name = "Current listing", Price = 10m });
        await harness.Session.SaveChangesAsync();
        await using var session = await harness.OpenSessionAsync(new SableSessionOptions { Tracking = DocumentTracking.IdentityOnly });
        var service = CreateService(session);
        var currentProduct = (await service.GetProductAsync(1, 100)).ShouldBeOfType<Result<ProductDocument?, AeroError>.Ok>().Value!;
        var currentListing = (await service.GetListingAsync(1, 10, 300)).ShouldBeOfType<Result<ProductListingDocument?, AeroError>.Ok>().Value!;

        var productResult = await service.UpdateProductAsync(1, 100, new ProductDocument
        {
            Name = "Stale overwrite",
            Sku = "STALE",
            StockQuantity = 9,
            IsActive = true,
            Version = currentProduct.Version + 1
        });
        productResult.ShouldBeOfType<Result<ProductDocument, AeroError>.Failure>().Error.ShouldBeOfType<AeroError.Conflict>();

        var listingResult = await service.UpdateListingAsync(1, 10, 300, new ProductListingDocument
        {
            ProductId = 100,
            Culture = "en-US",
            Slug = "stale",
            Name = "Stale overwrite",
            Price = 20m,
            Version = currentListing.Version + 1
        });
        listingResult.ShouldBeOfType<Result<ProductListingDocument, AeroError>.Failure>().Error.ShouldBeOfType<AeroError.Conflict>();

        await using var verify = await harness.OpenSessionAsync();
        (await verify.Query<ProductDocument>().FirstOrDefaultAsync(x => x.Id == 100))!.Name.ShouldBe("Current");
        (await verify.Query<ProductListingDocument>().FirstOrDefaultAsync(x => x.Id == 300))!.Name.ShouldBe("Current listing");
    }

    [Test]
    public async Task Concurrent_listing_association_and_product_delete_cannot_commit_an_orphan()
    {
        await using var harness = await CreateHarnessAsync();
        harness.Session.Store(new ProductDocument { Id = 100, TenantId = 1, Name = "Delete wins", Sku = "DELETE-WINS", StockQuantity = 3, IsActive = true });
        harness.Session.Store(new ProductDocument { Id = 101, TenantId = 1, Name = "Listing wins", Sku = "LISTING-WINS", StockQuantity = 3, IsActive = true });
        await harness.Session.SaveChangesAsync();

        await using (var staleListingSession = await harness.OpenSessionAsync(new SableSessionOptions { Tracking = DocumentTracking.IdentityOnly }))
        await using (var deleteSession = await harness.OpenSessionAsync(new SableSessionOptions { Tracking = DocumentTracking.IdentityOnly }))
        {
            var staleProduct = await staleListingSession.Query<ProductDocument>().FirstOrDefaultAsync(x => x.Id == 100);
            staleProduct.ShouldNotBeNull();
            var deleteService = CreateService(deleteSession);
            (await deleteService.DeleteProductAsync(1, 100)).ShouldBeOfType<Result<bool, AeroError>.Ok>().Value.ShouldBeTrue();

            staleProduct!.ModifiedOn = DateTimeOffset.UtcNow;
            staleListingSession.Store(staleProduct);
            var staleListing = Listing(100, "delete-wins");
            staleListing.Id = 300;
            staleListing.TenantId = 1;
            staleListing.SiteId = 10;
            staleListingSession.Store(staleListing);
            await Assert.That(async () => await staleListingSession.SaveChangesAsync()).Throws<ConcurrencyException>();
            staleListingSession.ClearChanges();
        }

        await using (var listingSession = await harness.OpenSessionAsync(new SableSessionOptions { Tracking = DocumentTracking.IdentityOnly }))
        await using (var staleDeleteSession = await harness.OpenSessionAsync(new SableSessionOptions { Tracking = DocumentTracking.IdentityOnly }))
        {
            var staleDeleteService = CreateService(staleDeleteSession);
            (await staleDeleteService.GetProductAsync(1, 101)).ShouldBeOfType<Result<ProductDocument?, AeroError>.Ok>().Value.ShouldNotBeNull();
            var listingService = CreateService(listingSession);
            (await listingService.CreateListingAsync(1, 10, Listing(101, "listing-wins"))).ShouldBeOfType<Result<ProductListingDocument, AeroError>.Ok>();

            var staleDelete = await staleDeleteService.DeleteProductAsync(1, 101);
            staleDelete.ShouldBeOfType<Result<bool, AeroError>.Failure>().Error.ShouldBeOfType<AeroError.Conflict>();
        }

        await using var verify = await harness.OpenSessionAsync();
        (await verify.Query<ProductDocument>().FirstOrDefaultAsync(x => x.Id == 100)).ShouldBeNull();
        (await verify.Query<ProductListingDocument>().FirstOrDefaultAsync(x => x.ProductId == 100)).ShouldBeNull();
        (await verify.Query<ProductDocument>().FirstOrDefaultAsync(x => x.Id == 101)).ShouldNotBeNull();
        (await verify.Query<ProductListingDocument>().FirstOrDefaultAsync(x => x.ProductId == 101)).ShouldNotBeNull();
    }

    [Test]
    public async Task Actual_unique_index_violation_is_classified_as_a_catalog_conflict()
    {
        await using var harness = await CreateHarnessAsync();
        harness.Session.Store(new ProductDocument
        {
            Id = 700,
            TenantId = 1,
            Name = "First",
            Sku = "DUPLICATE-SKU",
            StockQuantity = 1,
            IsActive = true
        });
        await harness.Session.SaveChangesAsync();

        await using var collisionSession = await harness.OpenSessionAsync();
        collisionSession.Store(new ProductDocument
        {
            Id = 701,
            TenantId = 1,
            Name = "Second",
            Sku = "DUPLICATE-SKU",
            StockQuantity = 1,
            IsActive = true
        });

        Exception? collision = null;
        try
        {
            await collisionSession.SaveChangesAsync();
        }
        catch (Exception exception)
        {
            collision = exception;
        }

        collision.ShouldNotBeNull();
        ProductService.IsUniqueConstraintConflict(collision!).ShouldBeTrue(collision!.ToString());
    }

    [Test]
    public async Task Catalog_conflict_result_maps_to_http_409()
    {
        await using var harness = await CreateHttpHarnessAsync(["create"]);
        var service = Substitute.For<IProductService>();
        service.CreateProductAsync(TenantId, Arg.Any<ProductDocument>(), Arg.Any<CancellationToken>())
            .Returns(Prelude.Fail<ProductDocument, AeroError>(AeroError.ConflictError("Duplicate SKU.")));
        await using var app = await CreateHttpAppAsync(harness.Session, service);
        using var request = ManagerRequest(HttpMethod.Post, "/api/v1/admin/commerce/catalog/products", includeCookie: true);
        request.Content = JsonContent.Create(new
        {
            name = "Duplicate",
            description = (string?)null,
            sku = "DUPLICATE-SKU",
            stockQuantity = 1,
            isActive = true,
            attributes = new Dictionary<string, string>(),
            tags = Array.Empty<string>()
        });

        using var response = await app.GetTestClient().SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    private static ProductService CreateService(SableTestHarness harness)
        => CreateService(harness.Session);

    private static ProductService CreateService(IDocumentSession session)
        => new(session, new ProductValidator(), new ProductListingValidator());

    private static ProductListingDocument Listing(long productId, string slug) => new()
    {
        ProductId = productId,
        Culture = "en-US",
        Slug = slug,
        Name = slug,
        Price = 10m
    };

    private static async Task<SableTestHarness> CreateHarnessAsync()
    {
        var harness = new SableTestHarness()
            .WithConfiguration(new CommerceModule().Configure)
            .WithSchema<SitesModel>(SchemaMode.Flexible)
            .WithSchema<UserSiteAssignment>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        return harness;
    }

    private static async Task<SableTestHarness> CreateHttpHarnessAsync(IReadOnlyList<string> permissions)
    {
        var harness = await CreateHarnessAsync();
        harness.Session.Store(new SitesModel { Id = SiteId, TenantId = TenantId, Name = "Store", IsEnabled = true });
        if (permissions.Count > 0)
        {
            harness.Session.Store(new UserSiteAssignment
            {
                Id = 900,
                UserId = UserId,
                SiteId = SiteId,
                Permissions = permissions.ToList()
            });
        }
        await harness.Session.SaveChangesAsync();
        return harness;
    }

    private static async Task<WebApplication> CreateHttpAppAsync(IDocumentSession session, IProductService service)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLogging();
        builder.Services.AddTestAuthentication();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<IQuerySession>(session);
        builder.Services.AddSingleton(session);
        builder.Services.AddScoped<IAuthorizationHandler, SitePermissionHandler>();
        builder.Services.AddAuthorization(options =>
        {
            foreach (var permission in new[] { "read", "create", "update", "delete" })
                options.AddPolicy($"site:{permission}", policy => policy.AddRequirements(new SitePermissionRequirement(permission)));
        });
        builder.Services.AddSingleton(service);
        builder.Services.AddScoped<ISiteContext, CookieSiteContext>();
        builder.Services.AddScoped<ICommerceManagerScopeResolver, CommerceManagerScopeResolver>();

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapCatalogApi();
        await app.StartAsync();
        return app;
    }

    private static HttpRequestMessage ManagerRequest(HttpMethod method, string path, bool includeCookie, bool isAdmin = false)
    {
        var request = new HttpRequestMessage(method, path).WithTestUser(UserId, isAdmin: isAdmin);
        if (includeCookie) request.Headers.Add("Cookie", $"AeroCms.SiteId={SiteId}");
        return request;
    }

    private static IHttpContextAccessor CreateHttpContextAccessor(long siteId)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $"AeroCms.SiteId={siteId}";
        return new HttpContextAccessor { HttpContext = context };
    }

    private sealed class CookieSiteContext(IHttpContextAccessor accessor) : ISiteContext
    {
        public long SiteId => long.TryParse(accessor.HttpContext?.Request.Cookies["AeroCms.SiteId"], out var siteId) ? siteId : 0;
        public long TenantId => 999;
    }
}
