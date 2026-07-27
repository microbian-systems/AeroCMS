using System.Net;
using System.Net.Http.Json;
using System.Globalization;
using System.Text.Json;
using System.Reflection;
using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Core.Tests.Integration;
using Aero.Cms.Modules.Commerce;
using Aero.Cms.Modules.Commerce.Areas.Commerce.Pages;
using Aero.Cms.Modules.Commerce.Basket.Models;
using Aero.Cms.Modules.Commerce.Basket.Services;
using Aero.Cms.Modules.Commerce.Catalog.Api;
using Aero.Cms.Modules.Commerce.Catalog.Models;
using Aero.Cms.Modules.Commerce.Catalog.Services;
using Aero.Cms.Modules.Commerce.Catalog.Validation;
using Aero.Cms.Modules.Commerce.Storefront;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace Aero.Cms.Core.Tests.Commerce;

public sealed class CommerceStorefrontTests
{
    [Test]
    public async Task Public_routes_are_anonymous_and_manager_policy_free()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(Substitute.For<IProductService>());
        builder.Services.AddSingleton(Substitute.For<ISiteContext>());
        builder.Services.AddSingleton(Substitute.For<ICommerceManagerScopeResolver>());
        await using var app = builder.Build();
        app.MapCatalogApi();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(x => x.Endpoints).OfType<RouteEndpoint>()
            .Where(x => x.RoutePattern.RawText?.StartsWith("/api/commerce/catalog", StringComparison.Ordinal) == true)
            .ToList();

        endpoints.Select(x => x.RoutePattern.RawText).ShouldBe([
            "/api/commerce/catalog/listings",
            "/api/commerce/catalog/listings/by-slug/{slug}",
            "/api/commerce/catalog/categories"
        ], ignoreOrder: true);
        endpoints.All(x => x.Metadata.GetMetadata<IAllowAnonymous>() is not null).ShouldBeTrue();
        endpoints.SelectMany(x => x.Metadata.GetOrderedMetadata<IAuthorizeData>()).ShouldBeEmpty();
    }

    [Test]
    public async Task Public_api_defaults_pagination_rejects_invalid_values_and_returns_an_empty_page()
    {
        var service = Substitute.For<IProductService>();
        service.SearchPublishedAsync(42, 10, Arg.Any<string>(), null, null, 0, 20, false, Arg.Any<CancellationToken>())
            .Returns(Prelude.Ok<(IReadOnlyList<ProductListingDocument>, long), AeroError>(([], 0)));
        await using var app = await CreatePublicAppAsync(service);
        var client = app.GetTestClient();

        (await client.GetAsync("/api/commerce/catalog/listings?skip=-1")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.GetAsync("/api/commerce/catalog/listings?take=0")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.GetAsync("/api/commerce/catalog/listings?take=101")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var response = await client.GetAsync("/api/commerce/catalog/listings");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PublicListingPage>();
        page.ShouldNotBeNull();
        page.Items.ShouldBeEmpty();
        page.TotalCount.ShouldBe(0);
        await service.Received(1).SearchPublishedAsync(42, 10, Arg.Any<string>(), null, null, 0, 20, false, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Public_api_accepts_exact_query_boundaries_and_rejects_overlength_values()
    {
        var service = Substitute.For<IProductService>();
        service.SearchPublishedAsync(42, 10, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), 0, 20, false, Arg.Any<CancellationToken>())
            .Returns(Prelude.Ok<(IReadOnlyList<ProductListingDocument>, long), AeroError>(([], 0)));
        await using var app = await CreatePublicAppAsync(service);
        var client = app.GetTestClient();
        var search200 = new string('s', 200);
        var category256 = new string('c', 256);

        (await client.GetAsync($"/api/commerce/catalog/listings?search={search200}&category={category256}")).StatusCode.ShouldBe(HttpStatusCode.OK);
        using var longSearch = await client.GetAsync($"/api/commerce/catalog/listings?search={new string('s', 201)}");
        using var longCategory = await client.GetAsync($"/api/commerce/catalog/listings?category={new string('c', 257)}");

        longSearch.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await longSearch.Content.ReadFromJsonAsync<CatalogValidationErrorResponse>())!.Errors.ShouldBe(["Search must be 200 characters or fewer."]);
        longCategory.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await longCategory.Content.ReadFromJsonAsync<CatalogValidationErrorResponse>())!.Errors.ShouldBe(["Category must be 256 characters or fewer."]);
    }

    [Test]
    public async Task Public_json_is_allowlisted_and_selected_site_cookie_cannot_change_host_scope()
    {
        var service = Substitute.For<IProductService>();
        service.SearchPublishedAsync(42, 10, Arg.Any<string>(), null, null, 0, 20, false, Arg.Any<CancellationToken>())
            .Returns(Prelude.Ok<(IReadOnlyList<ProductListingDocument>, long), AeroError>(([
                Listing(42, 10, 200, "safe", "Safe", category: "General")
            ], 1)));
        await using var app = await CreatePublicAppAsync(service);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/commerce/catalog/listings");
        request.Headers.Add("Cookie", "AeroCms.SiteId=999");
        using var response = await app.GetTestClient().SendAsync(request);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        var item = json.RootElement.GetProperty("items")[0];
        item.EnumerateObject().Select(x => x.Name).ShouldBe([
            "id", "slug", "name", "shortDescription", "description", "category", "imageUrl",
            "price", "compareAtPrice", "currency", "isFeatured", "isSubscription", "subscriptionIntervalDays"
        ], ignoreOrder: true);
        var serialized = item.GetRawText();
        serialized.ShouldNotContain("tenantId");
        serialized.ShouldNotContain("siteId");
        serialized.ShouldNotContain("stock");
        serialized.ShouldNotContain("version");
        serialized.ShouldNotContain("isPublished");
        await service.Received(1).SearchPublishedAsync(42, 10, Arg.Any<string>(), null, null, 0, 20, false, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Missing_or_noncanonical_slug_is_concealed_but_database_failure_is_not_a_404()
    {
        var service = Substitute.For<IProductService>();
        service.GetPublishedListingBySlugAsync(42, 10, Arg.Any<string>(), "missing", Arg.Any<CancellationToken>())
            .Returns(Prelude.Ok<ProductListingDocument?, AeroError>(null));
        service.GetPublishedListingBySlugAsync(42, 10, Arg.Any<string>(), "failed", Arg.Any<CancellationToken>())
            .Returns(Prelude.Fail<ProductListingDocument?, AeroError>(AeroError.DatabaseError("failed")));
        await using var app = await CreatePublicAppAsync(service);
        var client = app.GetTestClient();

        (await client.GetAsync("/api/commerce/catalog/listings/by-slug/Not-Canonical")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync("/api/commerce/catalog/listings/by-slug/missing")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync("/api/commerce/catalog/listings/by-slug/failed")).StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    [Test]
    public async Task Published_query_filters_before_count_and_page_and_isolates_site_culture_publication_and_active_product()
    {
        await using var harness = await CreateHarnessAsync();
        harness.Session.Store(new ProductDocument { Id = 100, TenantId = 1, Name = "Active", Sku = "ACTIVE", IsActive = true });
        harness.Session.Store(new ProductDocument { Id = 102, TenantId = 1, Name = "Also active", Sku = "ACTIVE-2", IsActive = true });
        harness.Session.Store(new ProductDocument { Id = 103, TenantId = 1, Name = "Unpublished", Sku = "UNPUBLISHED", IsActive = true });
        harness.Session.Store(new ProductDocument { Id = 101, TenantId = 1, Name = "Inactive", Sku = "INACTIVE", IsActive = false });
        harness.Session.Store(new ProductDocument { Id = 200, TenantId = 2, Name = "Foreign", Sku = "FOREIGN", IsActive = true });
        harness.Session.Store(Listing(1, 10, 100, "zulu", "Same", category: "Tools", id: 1, createdOn: new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)));
        harness.Session.Store(Listing(1, 10, 102, "alpha", "Same", category: "Tools", id: 2, createdOn: new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero)));
        harness.Session.Store(Listing(1, 11, 100, "other-site", "Same", category: "Tools", id: 3));
        harness.Session.Store(Listing(1, 10, 100, "wrong-culture", "Same", category: "Tools", culture: "fr-FR", id: 4));
        harness.Session.Store(Listing(1, 10, 103, "unpublished", "Same", category: "Tools", published: false, id: 5));
        harness.Session.Store(Listing(1, 10, 101, "inactive", "Same", category: "Tools", id: 6));
        harness.Session.Store(Listing(2, 20, 200, "foreign", "Same", category: "Tools", id: 7));
        await harness.Session.SaveChangesAsync();
        var service = CreateProductService(harness);

        var searchResult = await service.SearchPublishedAsync(1, 10, "en-US", search: "same", category: "tools", skip: 1, take: 1);
        var page = searchResult
            .ShouldBeOfType<Result<(IReadOnlyList<ProductListingDocument> Items, long TotalCount), AeroError>.Ok>().Value;

        page.TotalCount.ShouldBe(2);
        page.Items.Single().Id.ShouldBe(2);
        (await service.GetPublishedListingBySlugAsync(1, 10, "en-US", "inactive"))
            .ShouldBeOfType<Result<ProductListingDocument?, AeroError>.Ok>().Value.ShouldBeNull();
        var recent = (await service.GetRecentPublishedAsync(1, 10, "en-US", 2))
            .ShouldBeOfType<Result<IReadOnlyList<ProductListingDocument>, AeroError>.Ok>().Value;
        recent.Select(x => x.Id).ShouldBe([2, 1]);
    }

    [Test]
    public async Task Category_query_is_complete_beyond_one_hundred_results()
    {
        await using var harness = await CreateHarnessAsync();
        for (var index = 1; index <= 105; index++)
        {
            harness.Session.Store(new ProductDocument { Id = 1_000 + index, TenantId = 1, Name = $"Product {index}", Sku = $"SKU-{index}", IsActive = true });
            harness.Session.Store(Listing(1, 10, 1_000 + index, $"product-{index}", $"Product {index}", category: $"Category {index:D3}", id: 2_000 + index));
        }
        harness.Session.Store(new ProductDocument { Id = 1_106, TenantId = 1, Name = "No category", Sku = "NO-CATEGORY", IsActive = true });
        harness.Session.Store(new ProductDocument { Id = 1_107, TenantId = 1, Name = "Blank category", Sku = "BLANK-CATEGORY", IsActive = true });
        harness.Session.Store(Listing(1, 10, 1_106, "no-category", "No category", category: null, id: 2_106));
        harness.Session.Store(Listing(1, 10, 1_107, "blank-category", "Blank category", category: "   ", id: 2_107));
        await harness.Session.SaveChangesAsync();

        var result = await CreateProductService(harness).GetPublishedCategoriesAsync(1, 10, "en-US");

        var categories = result.ShouldBeOfType<Result<IReadOnlyList<string>, AeroError>.Ok>().Value;
        categories.Count.ShouldBe(105);
        categories.First().ShouldBe("Category 001");
        categories.Last().ShouldBe("Category 105");
    }

    [Test]
    public async Task Shop_home_combines_featured_and_recent_without_duplicates()
    {
        var products = Substitute.For<IProductService>();
        var site = Substitute.For<ISiteContext>();
        site.TenantId.Returns(42);
        site.SiteId.Returns(10);
        var featured = Listing(42, 10, 100, "featured", "Featured", id: 1);
        var recent = Listing(42, 10, 101, "recent", "Recent", id: 2);
        products.SearchPublishedAsync(42, 10, Arg.Any<string>(), null, null, 0, 6, true, Arg.Any<CancellationToken>())
            .Returns(Prelude.Ok<(IReadOnlyList<ProductListingDocument>, long), AeroError>(([featured], 1)));
        products.GetRecentPublishedAsync(42, 10, Arg.Any<string>(), 6, Arg.Any<CancellationToken>())
            .Returns(Prelude.Ok<IReadOnlyList<ProductListingDocument>, AeroError>([featured, recent]));
        var model = new ShopHomeModel(products, site) { PageContext = new PageContext { HttpContext = new DefaultHttpContext() } };

        await model.OnGetAsync(CancellationToken.None);

        model.FeaturedProducts.Select(x => x.Id).ShouldBe([1]);
        model.RecentProducts.Select(x => x.Id).ShouldBe([2]);
        model.LoadFailed.ShouldBeFalse();
    }

    [Test]
    public async Task Product_detail_uses_storefront_member_state_and_returns_direct_401_or_403_before_basket_mutation()
    {
        var products = Substitute.For<IProductService>();
        var baskets = Substitute.For<IBasketService>();
        var site = Substitute.For<ISiteContext>();
        site.TenantId.Returns(42);
        site.SiteId.Returns(10);
        var member = Substitute.For<IStorefrontMemberAccessor>();
        var model = new ProductDetailModel(products, baskets, site, member);

        member.GetAsync(Arg.Any<CancellationToken>()).Returns(new StorefrontMemberState(StorefrontMemberStateKind.Unauthenticated));
        (await model.OnPostAddToCartAsync(100, CancellationToken.None)).ShouldBeOfType<UnauthorizedResult>();
        member.GetAsync(Arg.Any<CancellationToken>()).Returns(new StorefrontMemberState(StorefrontMemberStateKind.NotCurrentSiteMember, 7));
        (await model.OnPostAddToCartAsync(100, CancellationToken.None)).ShouldBeOfType<StatusCodeResult>().StatusCode.ShouldBe(403);
        await baskets.DidNotReceiveWithAnyArgs().AddItemAsync(default, default, default, default, default, default!, default);

        member.GetAsync(Arg.Any<CancellationToken>()).Returns(new StorefrontMemberState(StorefrontMemberStateKind.Authorized, 7));
        baskets.AddItemAsync(42, 10, 7, 100, 1, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Prelude.Ok<BasketDocument, AeroError>(new BasketDocument()));
        (await model.OnPostAddToCartAsync(100, CancellationToken.None)).ShouldBeOfType<RedirectResult>().Url.ShouldBe("/shop/cart");
    }

    [Test]
    public async Task Page_editor_member_cart_journey_confirms_a_visible_listing_then_uses_the_scoped_basket_and_redirects_to_cart()
    {
        var products = Substitute.For<IProductService>();
        var baskets = Substitute.For<IBasketService>();
        var principal = Substitute.For<ICurrentPrincipal>();
        var site = Substitute.For<ISiteContext>();
        site.TenantId.Returns(42);
        site.SiteId.Returns(10);
        principal.PrincipalId.Returns(7);
        var listing = Listing(42, 10, 100, "journey-product", "Journey product", id: 100);
        products.GetPublishedListingAsync(42, 10, Arg.Any<string>(), 100, Arg.Any<CancellationToken>())
            .Returns(Prelude.Ok<ProductListingDocument?, AeroError>(listing));
        baskets.AddItemAsync(42, 10, 7, 100, 1, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Prelude.Ok<BasketDocument, AeroError>(new BasketDocument()));
        var model = new AddToCartModel(products, baskets, principal, site)
        {
            PageContext = new PageContext { HttpContext = new DefaultHttpContext() }
        };

        (await model.OnGetAsync(100, CancellationToken.None)).ShouldBeOfType<PageResult>();
        model.Product!.Name.ShouldBe("Journey product");
        model.CartPath.ShouldBe("/shop/cart?culture=" + Uri.EscapeDataString(CultureInfo.CurrentUICulture.Name));

        var redirect = (await model.OnPostAsync(100, CancellationToken.None)).ShouldBeOfType<RedirectResult>();
        redirect.Url.ShouldBe("/shop/cart?culture=" + Uri.EscapeDataString(CultureInfo.CurrentUICulture.Name));
        await baskets.Received(1).AddItemAsync(42, 10, 7, 100, 1, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Catalog_page_redirects_invalid_and_out_of_range_pages_without_losing_filters()
    {
        var products = Substitute.For<IProductService>();
        var site = Substitute.For<ISiteContext>();
        site.TenantId.Returns(42);
        site.SiteId.Returns(10);
        var model = new CatalogModel(products, site) { PageContext = new PageContext { HttpContext = new DefaultHttpContext() } };

        var invalid = (await model.OnGetAsync(" boot ", " gear ", 0)).ShouldBeOfType<RedirectToPageResult>();
        invalid.RouteValues!["search"].ShouldBe("boot");
        invalid.RouteValues["category"].ShouldBe("gear");

        products.SearchPublishedAsync(42, 10, Arg.Any<string>(), "boot", "gear", Arg.Any<int>(), 9, false, Arg.Any<CancellationToken>())
            .Returns(Prelude.Ok<(IReadOnlyList<ProductListingDocument>, long), AeroError>(([], 10)));
        var outOfRange = (await model.OnGetAsync("boot", "gear", 99)).ShouldBeOfType<RedirectToPageResult>();
        outOfRange.RouteValues!["page"].ShouldBe(2);
        outOfRange.RouteValues["search"].ShouldBe("boot");
        outOfRange.RouteValues["category"].ShouldBe("gear");
    }

    [Test]
    public void Catalog_pagination_window_is_bounded_at_the_beginning_middle_and_end()
    {
        CatalogPaginationWindow.Create(1, 20).Pages.ShouldBe([1, 2, 3, 4, 5]);
        CatalogPaginationWindow.Create(10, 20).Pages.ShouldBe([8, 9, 10, 11, 12]);
        CatalogPaginationWindow.Create(20, 20).Pages.ShouldBe([16, 17, 18, 19, 20]);
        CatalogPaginationWindow.Create(10, 20).Pages.Count.ShouldBeLessThanOrEqualTo(5);
        CatalogPaginationWindow.Create(1, 20).HasPrevious.ShouldBeFalse();
        CatalogPaginationWindow.Create(20, 20).HasNext.ShouldBeFalse();
    }

    [Test]
    public void Every_commerce_page_model_declares_private_no_store_response_cache_metadata()
    {
        var pageModels = new[]
        {
            typeof(ShopHomeModel), typeof(CatalogModel), typeof(ProductDetailModel), typeof(AddToCartModel), typeof(CartModel),
            typeof(CheckoutModel), typeof(OrdersModel), typeof(OrderDetailModel)
        };

        foreach (var pageModel in pageModels)
        {
            var policy = pageModel.GetCustomAttribute<ResponseCacheAttribute>();
            policy.ShouldNotBeNull($"{pageModel.Name} must prevent personalized HTML from entering shared caches.");
            policy.NoStore.ShouldBeTrue();
            policy.Location.ShouldBe(ResponseCacheLocation.None);
        }

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSingleton(Options.Create(new MvcOptions()));
        using var services = serviceCollection.BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = services };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor(), new ModelStateDictionary());
        var executing = new ActionExecutingContext(actionContext, [], new Dictionary<string, object?>(), new object());
        var responseCacheFilter = ((IFilterFactory)pageModels[0].GetCustomAttribute<ResponseCacheAttribute>()!).CreateInstance(services);
        ((IActionFilter)responseCacheFilter).OnActionExecuting(executing);
        httpContext.Response.Headers.CacheControl.ToString().ShouldContain("no-store");
    }

    private static async Task<WebApplication> CreatePublicAppAsync(IProductService service)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLogging();
        builder.Services.AddSingleton(service);
        builder.Services.AddSingleton(Substitute.For<ICommerceManagerScopeResolver>());
        var site = Substitute.For<ISiteContext>();
        site.TenantId.Returns(42);
        site.SiteId.Returns(10);
        builder.Services.AddSingleton(site);
        var app = builder.Build();
        app.MapCatalogApi();
        await app.StartAsync();
        return app;
    }

    private static ProductService CreateProductService(SableTestHarness harness)
        => new(harness.Session, new ProductValidator(), new ProductListingValidator());

    private static async Task<SableTestHarness> CreateHarnessAsync()
    {
        var harness = new SableTestHarness().WithConfiguration(new CommerceModule().Configure);
        await harness.InitializeAsync();
        return harness;
    }

    private static ProductListingDocument Listing(long tenantId, long siteId, long productId, string slug, string name,
        string? category = null, string culture = "en-US", bool published = true, long? id = null, DateTimeOffset? createdOn = null) => new()
    {
        Id = id ?? Math.Abs(slug.GetHashCode(StringComparison.Ordinal)),
        TenantId = tenantId,
        SiteId = siteId,
        ProductId = productId,
        Culture = culture,
        Slug = slug,
        Name = name,
        Category = category,
        Price = 10m,
        Currency = "USD",
        IsPublished = published,
        CreatedOn = createdOn ?? DateTimeOffset.UtcNow
    };
}
