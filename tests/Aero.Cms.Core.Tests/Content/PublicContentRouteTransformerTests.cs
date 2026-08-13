using System.Net;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Core.Content.Services;
using Aero.Cms.Modules.Content;
using Aero.Cms.Modules.Content.Routing;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

namespace Aero.Cms.Core.Tests.Content;

public sealed class PublicContentRouteTransformerTests
{
    [Test]
    public async Task Selector_rejects_unsupported_cultures_and_non_public_or_unknown_type_aliases()
    {
        var sites = Substitute.For<IPublicSiteRouteResolver>();
        sites.ResolveAsync("example.test", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<PublicSiteRouteScope?>(new PublicSiteRouteScope(7, "en-US", ["en-US", "fr-FR"])));
        var types = Substitute.For<IContentTypeService>();
        types.GetByAliasAsync(7, "animal", Arg.Any<CancellationToken>()).Returns(Ok(Type("animal", true)));
        types.GetByAliasAsync(7, "draft", Arg.Any<CancellationToken>()).Returns(Ok(Type("draft", false)));
        var selector = new PublicContentRouteTransformer(sites, types);

        (await SelectAsync(selector, "/de-DE/animal/wolf")).ShouldBeNull();
        (await SelectAsync(selector, "/en-US/unknown/wolf")).ShouldBeNull();
        (await SelectAsync(selector, "/en-US/draft/wolf")).ShouldBeNull();

        var values = await SelectAsync(selector, "/fr-fr/animal/wolf");
        values!["culture"].ShouldBe("fr-FR");
        values["page"].ShouldBe("/PublicContent");
    }

    [Test]
    public async Task Dynamic_endpoint_leaves_pages_posts_and_docs_paths_unclaimed_but_selects_registered_public_content()
    {
        await using var app = await CreateRoutingAppAsync();
        var client = app.GetTestClient();

        foreach (var path in new[]
                 {
                     "/en-US/pages/welcome",
                     "/en-US/posts/hello",
                     "/en-US/docs/getting-started",
                     "/de-DE/animal/wolf"
                 })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path)
            {
                Headers = { Host = "example.test" }
            };
            using var response = await client.SendAsync(request);
            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
            response.Headers.GetValues("X-Endpoint").Single().ShouldNotContain("PublicContent");
        }

        using var contentRequest = new HttpRequestMessage(HttpMethod.Get, "/fr-fr/animal/wolf")
        {
            Headers = { Host = "example.test" }
        };
        using var content = await client.SendAsync(contentRequest);
        content.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        content.Headers.GetValues("X-Endpoint").Single().ShouldContain("PublicContent");
        content.Headers.GetValues("X-Culture").Single().ShouldBe("fr-FR");
    }

    private static async Task<RouteValueDictionary?> SelectAsync(PublicContentRouteTransformer selector, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("example.test");
        var segments = path.Trim('/').Split('/');
        return await selector.TransformAsync(context, new RouteValueDictionary
        {
            ["culture"] = segments[0], ["typeAlias"] = segments[1], ["entrySlug"] = segments[2]
        });
    }

    private static async Task<WebApplication> CreateRoutingAppAsync()
    {
        var types = Substitute.For<IContentTypeService>();
        types.GetByAliasAsync(7, "animal", Arg.Any<CancellationToken>()).Returns(Ok(Type("animal", true)));
        types.GetByAliasAsync(7, Arg.Is<string>(alias => alias != "animal"), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<ContentTypeDefinition, AeroError>>(AeroError.NotFoundError("missing")));
        var sites = Substitute.For<IPublicSiteRouteResolver>();
        sites.ResolveAsync("example.test", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<PublicSiteRouteScope?>(new PublicSiteRouteScope(7, "en-US", ["en-US", "fr-FR"])));

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(types);
        builder.Services.AddSingleton(sites);
        builder.Services.AddTransient<PublicContentRouteTransformer>();
        builder.Services.AddRazorPages().AddApplicationPart(typeof(ContentModule).Assembly);

        var app = builder.Build();
        app.UseRouting();
        app.Use((HttpContext context, RequestDelegate _) =>
        {
            context.Response.Headers["X-Endpoint"] = context.GetEndpoint()?.DisplayName ?? string.Empty;
            context.Response.Headers["X-Culture"] = context.Request.RouteValues["culture"]?.ToString() ?? string.Empty;
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        });
        app.MapDynamicPageRoute<PublicContentRouteTransformer>("/{culture}/{typeAlias}/{entrySlug}");
        app.MapGet("/{culture}/pages/{slug}", () => Results.NoContent());
        app.MapGet("/{culture}/posts/{slug}", () => Results.NoContent());
        app.MapGet("/{culture}/docs/{slug}", () => Results.NoContent());
        app.MapRazorPages();
        await app.StartAsync();
        return app;
    }

    private static ContentTypeDefinition Type(string alias, bool isPublic) => new()
    {
        Id = 11, SiteId = 7, Alias = alias, Name = alias, AllowPublicUrl = isPublic
    };

    private static Task<Result<T, AeroError>> Ok<T>(T value) =>
        Task.FromResult<Result<T, AeroError>>(new Result<T, AeroError>.Ok(value));
}
