using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Aero.Cms.Modules.Commerce.Client;
using Aero.Cms.Modules.Commerce.Client.Models;
using Aero.Cms.Modules.Commerce.Client.Pages.Manager;
using Aero.Cms.Modules.Commerce.Client.Services;
using Aero.Cms.Shared.Routing;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace Aero.Cms.Core.Tests.Commerce;

public sealed class CommerceManagerClientTests
{
    [Test]
    public void Manager_pages_expose_only_the_canonical_manager_routes_and_require_authentication()
    {
        var pageTypes = new[] { typeof(CommerceOverview), typeof(Products), typeof(ProductEditor), typeof(Listings), typeof(ListingEditor) };
        var routes = pageTypes.SelectMany(type => type.GetCustomAttributes<RouteAttribute>()).Select(attribute => attribute.Template).ToHashSet();

        routes.ShouldBe(new HashSet<string>
        {
            "/manager/commerce",
            "/manager/commerce/products",
            "/manager/commerce/products/new",
            "/manager/commerce/products/{Id:long}",
            "/manager/commerce/listings",
            "/manager/commerce/listings/new",
            "/manager/commerce/listings/{Id:long}"
        }, ignoreOrder: true);
        routes.ShouldNotContain("/admin/commerce/products");
        pageTypes.All(type => type.GetCustomAttribute<AuthorizeAttribute>() is not null).ShouldBeTrue();
    }

    [Test]
    public async Task Manager_client_calls_only_the_corrected_admin_catalog_prefix()
    {
        var handler = new CapturingHandler("{\"items\":[],\"totalCount\":0}");
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://aero.test/") };
        var commerce = new CommerceManagerHttpClient(client, NullLogger<CommerceManagerHttpClient>.Instance);

        var result = await commerce.GetProductsAsync("red shirt", 20, 10);

        result.ShouldBeOfType<Result<ManagerCatalogPage<ManagerProductDto>, AeroError>.Ok>();
        handler.Requests.Single().Uri.PathAndQuery.ShouldBe("/api/v1/admin/commerce/catalog/products?skip=20&take=10&search=red%20shirt");
        handler.Requests.Single().Uri.PathAndQuery.ShouldNotContain("/api/commerce/catalog/manager");
    }

    [Test]
    public async Task Manager_client_CRUD_uses_canonical_routes_and_preserves_request_versions()
    {
        var handler = new CapturingHandler(
            "{\"id\":7,\"name\":\"Boot\",\"description\":null,\"sku\":\"BOOT-7\",\"stockQuantity\":3,\"isActive\":true,\"attributes\":{},\"tags\":[],\"version\":18}",
            "{\"id\":9,\"productId\":7,\"culture\":\"en-US\",\"slug\":\"boot\",\"name\":\"Boot\",\"shortDescription\":null,\"description\":null,\"category\":null,\"imageUrl\":null,\"price\":25.00,\"compareAtPrice\":null,\"currency\":\"USD\",\"isPublished\":true,\"isFeatured\":false,\"version\":30}",
            string.Empty);
        var commerce = new CommerceManagerHttpClient(new HttpClient(handler) { BaseAddress = new Uri("https://aero.test/") }, NullLogger<CommerceManagerHttpClient>.Instance);

        await commerce.CreateProductAsync(new ManagerProductRequest("Boot", null, "BOOT-7", 3, true, [], [], 17));
        await commerce.UpdateListingAsync(9, new ManagerListingRequest(7, "en-US", "boot", "Boot", null, null, null, null, 25m, null, true, false, 29));
        await commerce.DeleteListingAsync(9);

        handler.Requests.Select(request => (request.Method, request.Uri.PathAndQuery)).ShouldBe([
            (HttpMethod.Post, "/api/v1/admin/commerce/catalog/products"),
            (HttpMethod.Put, "/api/v1/admin/commerce/catalog/listings/9"),
            (HttpMethod.Delete, "/api/v1/admin/commerce/catalog/listings/9")
        ]);
        JsonSerializer.Deserialize<ManagerProductRequest>(handler.Requests[0].Body!, JsonOptions)!.Version.ShouldBe(17);
        JsonSerializer.Deserialize<ManagerListingRequest>(handler.Requests[1].Body!, JsonOptions)!.Version.ShouldBe(29);
    }

    [Test]
    public void Editor_mapping_and_validation_match_the_server_concurrency_and_USD_contracts()
    {
        ProductEditorModel.From(new ManagerProductDto(7, "Boot", null, "BOOT-7", 3, true, new Dictionary<string, string>(), [], 41))
            .ToRequest().Version.ShouldBe(41);
        ListingEditorModel.From(new ManagerListingDto(9, 7, "en-US", "boot", "Boot", null, null, null, null, 25m, 25m, "USD", true, false, 53))
            .ToRequest().Version.ShouldBe(53);

        var validator = new ListingEditorModelValidator();
        var valid = new ListingEditorModel { ProductId = 7, Culture = "en-US", Slug = "Boot Special", Name = "Boot", Price = 25m, CompareAtPrice = 25m };
        validator.Validate(valid).IsValid.ShouldBeTrue();
        valid.Price = 0m;
        validator.Validate(valid).IsValid.ShouldBeFalse();
        valid.Price = 25.001m;
        validator.Validate(valid).IsValid.ShouldBeFalse();
        valid.Price = 1_000_000_001m;
        validator.Validate(valid).IsValid.ShouldBeFalse();
    }

    [Test]
    public void Listing_editor_normalizes_the_same_route_safe_slug_as_the_server()
    {
        var editor = new ListingEditorModel { ProductId = 7, Culture = "en-US", Slug = "  Boot & Trail__2026  ", Name = "Boot", Price = 25m };

        var clientSlug = editor.ToRequest().Slug;

        clientSlug.ShouldBe("boot-trail-2026");
        clientSlug.ShouldBe(Aero.Cms.Modules.Commerce.Catalog.Models.CatalogSlug.Normalize(editor.Slug));
        new ListingEditorModelValidator().Validate(editor).IsValid.ShouldBeTrue();
        editor.Slug = "---";
        new ListingEditorModelValidator().Validate(editor).IsValid.ShouldBeFalse();
    }

    [Test]
    public async Task Listing_product_picker_preserves_a_selected_product_beyond_the_first_search_page()
    {
        var client = Substitute.For<ICommerceManagerClient>();
        var firstPage = Enumerable.Range(1, 50).Select(id => Product(id)).ToList();
        client.GetProductsAsync(null, 0, 50, Arg.Any<CancellationToken>())
            .Returns(new Result<ManagerCatalogPage<ManagerProductDto>, AeroError>.Ok(new ManagerCatalogPage<ManagerProductDto>(firstPage, 101)));
        client.GetProductAsync(101, Arg.Any<CancellationToken>())
            .Returns(new Result<ManagerProductDto, AeroError>.Ok(Product(101)));

        var picker = new ListingProductPicker(client);
        var result = await picker.SearchAsync(null, 101);

        result.ShouldBeOfType<Result<IReadOnlyList<ManagerProductDto>, AeroError>.Ok>();
        picker.Products.Count.ShouldBe(51);
        picker.Products.First().Id.ShouldBe(101);
    }

    [Test]
    public void Commerce_registration_explicitly_contributes_its_route_assembly()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAeroCommerceClient();

        using var provider = services.BuildServiceProvider();
        var routeAssembly = provider.GetServices<IManagerRouteAssemblyProvider>().Single().Assembly;

        routeAssembly.ShouldBe(typeof(CommerceOverview).Assembly);
        provider.GetRequiredService<ICommerceManagerClient>().ShouldBeOfType<CommerceManagerHttpClient>();
    }

    private static ManagerProductDto Product(int id) => new(id, $"Product {id}", null, $"SKU-{id}", id, true, new Dictionary<string, string>(), [], 1);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed class CapturingHandler(params string[] responses) : HttpMessageHandler
    {
        private readonly Queue<string> responses = new(responses);
        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new CapturedRequest(request.Method, request.RequestUri!, request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responses.Dequeue(), Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed record CapturedRequest(HttpMethod Method, Uri Uri, string? Body);
}
