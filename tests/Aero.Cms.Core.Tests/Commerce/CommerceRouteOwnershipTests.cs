using System.Net;
using Aero.Cms.Modules.Commerce;
using Aero.Cms.Modules.Pages;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Aero.Cms.Core.Tests.Commerce;

public sealed class CommerceRouteOwnershipTests
{
    [Test]
    public async Task Published_page_document_routes_win_over_commerce_for_public_storefront_paths()
    {
        await using var app = await CreateRoutingAppAsync();
        var client = app.GetTestClient();

        foreach (var (path, slug) in new[]
                 {
                     ("/shop", "shop"),
                     ("/shop/search", "shop/search"),
                     ("/shop/products", "shop/products"),
                     ("/shop/products/seeded-product", "shop/products/seeded-product")
                 })
        {
            using var response = await client.GetAsync(path);

            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
            response.Headers.GetValues("X-Test-Page-Area").Single().ShouldBe("Cms");
            response.Headers.GetValues("X-Test-Slug").Single().ShouldBe(slug);
            response.Headers.GetValues("X-Test-Order-Id").Single().ShouldBeEmpty();
        }
    }

    [Test]
    public async Task Private_commerce_routes_take_precedence_over_the_cms_catch_all()
    {
        await using var app = await CreateRoutingAppAsync();
        var client = app.GetTestClient();

        foreach (var (path, orderId) in new[]
                 {
                     ("/shop/cart", string.Empty),
                     ("/shop/checkout", string.Empty),
                     ("/shop/orders", string.Empty),
                     ("/shop/orders/123", "123"),
                     ("/shop/account", string.Empty)
                 })
        {
            using var response = await client.GetAsync(path);

            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
            response.Headers.GetValues("X-Test-Page-Area").Single().ShouldBe("Commerce");
            response.Headers.GetValues("X-Test-Slug").Single().ShouldBeEmpty();
            response.Headers.GetValues("X-Test-Order-Id").Single().ShouldBe(orderId);
        }
    }

    [Test]
    public async Task Commerce_has_no_public_catalog_selector_that_can_bypass_site_scoped_page_resolution()
    {
        await using var app = await CreateRoutingAppAsync();

        var publicCatalogTemplates = new HashSet<string>(StringComparer.Ordinal)
        {
            "/shop",
            "/shop/search",
            "/shop/products",
            "/shop/products/{slug}"
        };

        var commerceTemplates = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<PageActionDescriptor>()?.AreaName == "Commerce")
            .Select(endpoint => NormalizeTemplate(endpoint.RoutePattern.RawText))
            .ToList();

        commerceTemplates.ShouldNotContain(template => publicCatalogTemplates.Contains(template));
        commerceTemplates.ShouldContain("/shop/cart");
        commerceTemplates.ShouldContain("/shop/checkout");
        commerceTemplates.ShouldContain("/shop/orders");
        commerceTemplates.ShouldContain("/shop/orders/{id:long}");
        commerceTemplates.ShouldContain("/shop/account");
    }

    private static async Task<WebApplication> CreateRoutingAppAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLogging();
        new PagesModule().ConfigureServices(builder.Services);
        new CommerceModule().ConfigureServices(builder.Services);

        var app = builder.Build();
        app.UseRouting();
        app.Use((HttpContext context, RequestDelegate _) =>
        {
            var page = context.GetEndpoint()?.Metadata.GetMetadata<PageActionDescriptor>();
            context.Response.Headers["X-Test-Page-Area"] = page?.AreaName ?? string.Empty;
            context.Response.Headers["X-Test-Slug"] = context.Request.RouteValues["slug"]?.ToString() ?? string.Empty;
            context.Response.Headers["X-Test-Order-Id"] = context.Request.RouteValues["id"]?.ToString() ?? string.Empty;
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        });
        app.MapRazorPages();
        await app.StartAsync();
        return app;
    }

    private static string NormalizeTemplate(string? template)
        => string.IsNullOrWhiteSpace(template) ? "/" : "/" + template.TrimStart('/');
}
