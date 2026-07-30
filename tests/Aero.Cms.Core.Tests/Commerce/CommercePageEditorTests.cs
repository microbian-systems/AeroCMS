using System.Text.Json;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Commerce;
using Aero.Cms.Modules.Commerce.Catalog.Models;
using Aero.Cms.Modules.Commerce.Catalog.Services;
using Aero.Cms.Modules.Commerce.Catalog.Validation;
using Aero.Cms.Modules.Commerce.Data;
using Aero.Cms.Modules.Commerce.PageEditor;
using Aero.Cms.Modules.Commerce.Storefront;
using Aero.Cms.Modules.Pages;
using Aero.Cms.Modules.Pages.Rendering;
using Aero.Core;
using Aero.Core.Railway;
using Aero.Services.Images;
using AeroDB.Sable;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace Aero.Cms.Core.Tests.Commerce;

public sealed class CommercePageEditorTests
{
    [Test]
    public async Task Catalog_fragment_renders_only_current_site_culture_published_active_listings()
    {
        await using var harness = await CreateCatalogHarnessAsync();
        await SeedCatalogIsolationFixtureAsync(harness);
        using var services = CreateFragmentServices(harness);
        var provider = new CommerceCatalogPageRegisteredFragmentProvider(
            services.GetRequiredService<IServiceScopeFactory>(),
            services.GetRequiredService<IHttpContextAccessor>());

        var result = await provider.RenderAsync(
            Fragment("commerce.catalog", ("take", 24), ("featuredOnly", false)),
            new PageFragmentRenderContext { SiteId = 10, Culture = "en-US" });

        var html = RequireOk(result);
        html.ShouldContain("Current storefront");
        html.ShouldNotContain("Other site");
        html.ShouldNotContain("French storefront");
        html.ShouldNotContain("Inactive product");
        html.ShouldNotContain("Unpublished product");
        html.ShouldNotContain("Other tenant");
        html.ShouldContain("action=\"/en-us/shop\"");
        html.ShouldContain("name=\"search\"");
    }

    [Test]
    public async Task Catalog_fragment_searches_from_shop_index_and_discloses_recurring_terms()
    {
        await using var harness = await CreateCatalogHarnessAsync();
        await SeedCatalogIsolationFixtureAsync(harness);
        var recurring = (await harness.Session.Query<ProductListingDocument>()
                .Where(listing => listing.Id == 301)
                .ToListAsync())
            .Single();
        recurring.SubscriptionOffer = new SubscriptionOffer { IntervalDays = 30, StripePriceId = "price_current" };
        harness.Session.Store(recurring);
        await harness.Session.SaveChangesAsync();
        using var services = CreateFragmentServices(harness, "Current");
        var provider = new CommerceCatalogPageRegisteredFragmentProvider(
            services.GetRequiredService<IServiceScopeFactory>(),
            services.GetRequiredService<IHttpContextAccessor>());

        var result = await provider.RenderAsync(
            Fragment("commerce.catalog", ("take", 24), ("featuredOnly", true)),
            new PageFragmentRenderContext { SiteId = 10, Culture = "en-US" });

        var html = RequireOk(result);
        html.ShouldContain("value=\"Current\"");
        html.ShouldContain("Current storefront");
        html.ShouldContain("Subscription:</strong> renews every 30 days until cancelled.");
        html.ShouldNotContain("Other site");
    }

    [Test]
    public async Task Search_and_product_fragments_use_current_site_and_culture_not_request_or_slug_lookalikes()
    {
        await using var harness = await CreateCatalogHarnessAsync();
        await SeedCatalogIsolationFixtureAsync(harness);
        using var services = CreateFragmentServices(harness, "Current");
        var scopes = services.GetRequiredService<IServiceScopeFactory>();
        var accessor = services.GetRequiredService<IHttpContextAccessor>();
        var search = new CommerceSearchPageRegisteredFragmentProvider(scopes, accessor);
        var product = new CommerceProductPageRegisteredFragmentProvider(scopes, accessor);

        var searchResult = await search.RenderAsync(
            Fragment("commerce.search", ("take", 12)),
            new PageFragmentRenderContext { SiteId = 10, Culture = "en-US" });
        var productResult = await product.RenderAsync(
            Fragment("commerce.product", ("slug", "current-storefront")),
            new PageFragmentRenderContext { SiteId = 10, Culture = "en-US" });
        var foreignCultureProduct = await product.RenderAsync(
            Fragment("commerce.product", ("slug", "french-storefront")),
            new PageFragmentRenderContext { SiteId = 10, Culture = "en-US" });

        RequireOk(searchResult).ShouldContain("Current storefront");
        RequireOk(productResult).ShouldContain("Current storefront");
        RequireOk(foreignCultureProduct).ShouldContain("Product unavailable.");
    }

    [Test]
    public async Task Commerce_fragments_build_public_links_with_the_active_culture_route()
    {
        await using var harness = await CreateCatalogHarnessAsync();
        await SeedCatalogIsolationFixtureAsync(harness);
        using var services = CreateFragmentServices(harness);
        var scopes = services.GetRequiredService<IServiceScopeFactory>();
        var accessor = services.GetRequiredService<IHttpContextAccessor>();
        var catalog = new CommerceCatalogPageRegisteredFragmentProvider(scopes, accessor);
        var search = new CommerceSearchPageRegisteredFragmentProvider(scopes, accessor);
        var product = new CommerceProductPageRegisteredFragmentProvider(scopes, accessor);
        var frenchContext = new PageFragmentRenderContext { SiteId = 10, Culture = "fr-FR" };

        var catalogHtml = RequireOk(await catalog.RenderAsync(Fragment("commerce.catalog", ("take", 12)), frenchContext));
        var searchHtml = RequireOk(await search.RenderAsync(Fragment("commerce.search", ("take", 12)), frenchContext));
        var productHtml = RequireOk(await product.RenderAsync(Fragment("commerce.product", ("slug", "french-storefront")), frenchContext));

        catalogHtml.ShouldContain("href=\"/fr-fr/shop/products/french-storefront\"");
        catalogHtml.ShouldContain("action=\"/fr-fr/shop\"");
        searchHtml.ShouldContain("action=\"/fr-fr/shop/search\"");
        productHtml.ShouldContain("href=\"/fr-fr/shop/products/french-storefront\"");
    }

    [Test]
    public async Task Public_product_fragment_offers_an_authorized_member_a_private_cart_journey_without_antiforgery_material()
    {
        await using var harness = await CreateCatalogHarnessAsync();
        await SeedCatalogIsolationFixtureAsync(harness);
        using var services = CreateFragmentServices(
            harness,
            member: new StorefrontMemberState(StorefrontMemberStateKind.Authorized, 77));
        var product = new CommerceProductPageRegisteredFragmentProvider(
            services.GetRequiredService<IServiceScopeFactory>(),
            services.GetRequiredService<IHttpContextAccessor>());

        var html = (await product.RenderAsync(
                Fragment("commerce.product", ("slug", "current-storefront")),
                new PageFragmentRenderContext { SiteId = 10, Culture = "en-US" }))
            .ShouldBeOfType<Result<string>.Ok>().Value;

        html.ShouldContain("href=\"/shop/cart/add?culture=en-US&amp;listingId=301\"");
        html.ShouldContain("href=\"/shop/cart?culture=en-US\"");
        html.ShouldContain("Add to shopping bag");
        html.ShouldNotContain("__RequestVerificationToken");
    }

    [Test]
    public async Task Seed_service_creates_idempotent_native_pages_per_site_and_culture_through_page_services()
    {
        await using var harness = await CreateCatalogHarnessAsync();
        var site = new SitesModel
        {
            Id = 10,
            TenantId = 1,
            Name = "Storefront",
            DefaultCulture = "en-US",
            SupportedCultures = ["en-US", "fr-FR"]
        };
        harness.Session.Store(site);
        harness.Session.Store(Listing(1, 10, 101, "en-product", "English product", "en-US", id: 201));
        harness.Session.Store(Listing(1, 10, 102, "fr-product", "French product", "fr-FR", id: 202));
        harness.Session.Store(Listing(1, 10, 103, "hidden-product", "Hidden product", "en-US", published: false, id: 203));
        await harness.Session.SaveChangesAsync();

        var pageContentService = Substitute.For<IPageContentService>();
        pageContentService.SaveAsync(Arg.Any<PageDocument>(), site.Id, Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                var page = call.ArgAt<PageDocument>(0);
                harness.Session.Store(page);
                await harness.Session.SaveChangesAsync();
                return Prelude.Ok<PageDocument, AeroError>(page);
            });
        var publishing = Substitute.For<IPagePublishingWorkflowService>();
        publishing.PublishNowAsync(Arg.Any<long>(), site.Id, Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                var page = await harness.Session.LoadAsync<PageDocument>(call.ArgAt<long>(0));
                page.ShouldNotBeNull();
                page.PublishDraftContent(DateTimeOffset.UtcNow);
                harness.Session.Store(page);
                await harness.Session.SaveChangesAsync();
                return Prelude.Ok<bool, AeroError>(true);
            });
        var seed = new CommerceSeedService(
            Substitute.For<IPexelsService>(),
            harness.Session,
            pageContentService,
            publishing,
            NullLogger<CommerceSeedService>.Instance);

        await seed.SeedAsync(site.Id);

        var editedShop = (await harness.Session.Query<PageDocument>()
                .Where(page => page.SiteId == site.Id && page.Culture == "en-US" && page.Path == "/shop")
                .ToListAsync())
            .Single();
        editedShop.Title = "Merchant edited shop";
        editedShop.DraftContent.Root.Attributes["data-merchant-edit"] = "draft-preserved";
        editedShop.PublishedContent.ShouldNotBeNull().Root.Attributes["data-merchant-edit"] = "published-preserved";
        editedShop.DraftComposition = WithFirstFragmentParameter(
            editedShop.DraftComposition,
            "merchantNote",
            JsonSerializer.SerializeToElement("draft-preserved"));
        editedShop.PublishedComposition = WithFirstFragmentParameter(
            editedShop.PublishedComposition.ShouldNotBeNull(),
            "merchantNote",
            JsonSerializer.SerializeToElement("published-preserved"));
        harness.Session.Store(editedShop);
        await harness.Session.SaveChangesAsync();

        var draftSnapshot = JsonSerializer.Serialize(editedShop.DraftContent);
        var draftCompositionSnapshot = JsonSerializer.Serialize(editedShop.DraftComposition);
        var publishedSnapshot = JsonSerializer.Serialize(editedShop.PublishedContent);
        var publishedCompositionSnapshot = JsonSerializer.Serialize(editedShop.PublishedComposition);

        await seed.SeedAsync(site.Id);

        var pages = await harness.Session.Query<PageDocument>()
            .Where(page => page.SiteId == site.Id)
            .ToListAsync();
        pages.Count.ShouldBe(8);
        foreach (var culture in new[] { "en-US", "fr-FR" })
        {
            var culturePages = pages.Where(page => page.Culture == culture).ToList();
            culturePages.Select(page => page.Path).ShouldBe(
                ["/shop", "/shop/search", "/shop/products", $"/shop/products/{(culture == "en-US" ? "en-product" : "fr-product")}"],
                ignoreOrder: true);
            culturePages.All(page => page.Id > 0).ShouldBeTrue();
            culturePages.All(page => page.DraftContent is not null && page.DraftComposition.RegisteredFragments.Count == 1).ShouldBeTrue();
            culturePages.All(page => page.PublishedContent is not null && page.PublishedComposition is not null).ShouldBeTrue();
            culturePages.All(page => page.PublicationState == ContentPublicationState.Published).ShouldBeTrue();
        }

        var frenchShop = pages.Single(page => page.Culture == "fr-FR" && page.Path == "/shop");
        JsonSerializer.Serialize(frenchShop.DraftContent).ShouldContain("/fr-fr/shop");
        JsonSerializer.Serialize(frenchShop.DraftContent).ShouldContain("/fr-fr/shop/products");
        JsonSerializer.Serialize(frenchShop.DraftContent).ShouldContain("/fr-fr/shop/search");

        var persistedShop = pages.Single(page => page.Id == editedShop.Id);
        persistedShop.Title.ShouldBe("Merchant edited shop");
        ShouldHaveEquivalentJson(JsonSerializer.Serialize(persistedShop.DraftContent), draftSnapshot);
        ShouldHaveEquivalentJson(JsonSerializer.Serialize(persistedShop.DraftComposition), draftCompositionSnapshot);
        ShouldHaveEquivalentJson(JsonSerializer.Serialize(persistedShop.PublishedContent), publishedSnapshot);
        ShouldHaveEquivalentJson(JsonSerializer.Serialize(persistedShop.PublishedComposition), publishedCompositionSnapshot);

        var seededProduct = pages.Single(page => page.Culture == "en-US" && page.Path == "/shop/products/en-product");
        var retiredListing = (await harness.Session.Query<ProductListingDocument>()
                .Where(listing => listing.Id == 201)
                .ToListAsync())
            .Single();
        retiredListing.IsPublished = false;
        harness.Session.Store(retiredListing);
        await harness.Session.SaveChangesAsync();

        // The page was published while the listing was visible. After unpublish it must not
        // retain product title, summary, SEO text, or static content; visibility is owned by
        // the runtime fragment instead.
        seededProduct.Title.ShouldBe("Storefront product");
        seededProduct.Summary.ShouldBe("Product availability and details are shown from the current storefront catalog.");
        seededProduct.SeoTitle.ShouldBe("Storefront product");
        seededProduct.SeoDescription.ShouldBe("Product availability and details are shown from the current storefront catalog.");
        JsonSerializer.Serialize(seededProduct.PublishedContent).ShouldNotContain("English product");
        JsonSerializer.Serialize(seededProduct.PublishedContent).ShouldNotContain("en-product");

        await pageContentService.Received(8).SaveAsync(
            Arg.Is<PageDocument>(page => page.SiteId == site.Id), site.Id, Arg.Any<CancellationToken>());
        await publishing.Received(8).PublishNowAsync(Arg.Any<long>(), site.Id, Arg.Any<CancellationToken>());
    }

    private static async Task<SableTestHarness> CreateCatalogHarnessAsync()
    {
        var harness = new SableTestHarness()
            .WithSchema<SitesModel>()
            .WithSchema<PageDocument>()
            .WithConfiguration(new CommerceModule().Configure);
        await harness.InitializeAsync();
        return harness;
    }

    private static void ShouldHaveEquivalentJson(string actual, string expected)
    {
        using var actualDocument = JsonDocument.Parse(actual);
        using var expectedDocument = JsonDocument.Parse(expected);
        JsonElement.DeepEquals(actualDocument.RootElement, expectedDocument.RootElement).ShouldBeTrue();
    }

    private static string RequireOk(Result<string> result)
        => result switch
        {
            Result<string>.Ok ok => ok.Value,
            Result<string>.Failure failure => throw new InvalidOperationException(
                $"Commerce fragment failed: {failure.Error}"),
            _ => throw new InvalidOperationException("Commerce fragment returned an unknown result.")
        };

    private static ServiceProvider CreateFragmentServices(
        SableTestHarness harness,
        string? search = null,
        StorefrontMemberState? member = null)
    {
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        if (search is not null)
            accessor.HttpContext.Request.QueryString = new QueryString($"?search={search}");
        var services = new ServiceCollection();
        services.AddSingleton<IDocumentSession>(harness.Session);
        services.AddSingleton<IProductService>(new ProductService(harness.Session, new ProductValidator(), new ProductListingValidator()));
        services.AddSingleton<IHttpContextAccessor>(accessor);
        if (member is not null)
        {
            var accessorService = Substitute.For<IStorefrontMemberAccessor>();
            accessorService.GetAsync(Arg.Any<CancellationToken>()).Returns(member);
            services.AddSingleton(accessorService);
        }
        return services.BuildServiceProvider();
    }

    private static async Task SeedCatalogIsolationFixtureAsync(SableTestHarness harness)
    {
        harness.Session.Store(new SitesModel { Id = 10, TenantId = 1, DefaultCulture = "en-US" });
        harness.Session.Store(new SitesModel { Id = 11, TenantId = 1, DefaultCulture = "en-US" });
        harness.Session.Store(new SitesModel { Id = 20, TenantId = 2, DefaultCulture = "en-US" });
        harness.Session.Store(new ProductDocument { Id = 101, TenantId = 1, Name = "Current storefront", Sku = "CURRENT", IsActive = true });
        harness.Session.Store(new ProductDocument { Id = 102, TenantId = 1, Name = "Other site", Sku = "OTHER-SITE", IsActive = true });
        harness.Session.Store(new ProductDocument { Id = 103, TenantId = 1, Name = "French storefront", Sku = "FRENCH", IsActive = true });
        harness.Session.Store(new ProductDocument { Id = 104, TenantId = 1, Name = "Inactive product", Sku = "INACTIVE", IsActive = false });
        harness.Session.Store(new ProductDocument { Id = 105, TenantId = 1, Name = "Unpublished product", Sku = "UNPUBLISHED", IsActive = true });
        harness.Session.Store(new ProductDocument { Id = 201, TenantId = 2, Name = "Other tenant", Sku = "OTHER-TENANT", IsActive = true });
        harness.Session.Store(Listing(1, 10, 101, "current-storefront", "Current storefront", "en-US", id: 301));
        harness.Session.Store(Listing(1, 11, 102, "other-site", "Other site", "en-US", id: 302));
        harness.Session.Store(Listing(1, 10, 103, "french-storefront", "French storefront", "fr-FR", id: 303));
        harness.Session.Store(Listing(1, 10, 104, "inactive-product", "Inactive product", "en-US", id: 304));
        harness.Session.Store(Listing(1, 10, 105, "unpublished-product", "Unpublished product", "en-US", published: false, id: 305));
        harness.Session.Store(Listing(2, 20, 201, "other-tenant", "Other tenant", "en-US", id: 306));
        await harness.Session.SaveChangesAsync();
    }

    private static ProductListingDocument Listing(
        long tenantId, long siteId, long productId, string slug, string name, string culture,
        bool published = true, long? id = null) => new()
    {
        Id = id ?? productId,
        TenantId = tenantId,
        SiteId = siteId,
        ProductId = productId,
        Culture = culture,
        Slug = slug,
        Name = name,
        ShortDescription = name,
        Price = 10m,
        Currency = "USD",
        IsPublished = published
    };

    private static PageRegisteredFragment Fragment(string key, params (string Name, object Value)[] parameters)
        => new()
        {
            Key = key,
            Parameters = parameters.ToDictionary(
                parameter => parameter.Name,
                parameter => JsonSerializer.SerializeToElement(parameter.Value),
                StringComparer.Ordinal)
        };

    private static PageCompositionDocument WithFirstFragmentParameter(
        PageCompositionDocument composition,
        string name,
        JsonElement value)
    {
        var fragment = composition.RegisteredFragments[0];
        var parameters = fragment.Parameters.ToDictionary(
            parameter => parameter.Key,
            parameter => parameter.Value.Clone(),
            StringComparer.Ordinal);
        parameters[name] = value.Clone();

        return composition with
        {
            RegisteredFragments = composition.RegisteredFragments
                .Select((candidate, index) => index == 0
                    ? fragment with { Parameters = parameters }
                    : candidate)
                .ToArray()
        };
    }
}
