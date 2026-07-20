using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Ai;
using Aero.Cms.Abstractions.Audit;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Modules.Posts;
using Aero.Cms.Modules.Posts.Areas.Api.v1;
using Aero.Core;
using Aero.Core.Http;
using AeroDB.Sable;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Core.Tests.Integration;

public sealed class PostsAuthorizationMetadataTests
{
    [Test]
    [Arguments("GET", "/api/v1/admin/blogs/", "site:read")]
    [Arguments("GET", "/api/v1/admin/blogs/translation-groups", "site:read")]
    [Arguments("GET", "/api/v1/admin/blogs/{id:long}", "site:read")]
    [Arguments("GET", "/api/v1/admin/blogs/{id:long}/translations", "site:read")]
    [Arguments("GET", "/api/v1/admin/blogs/slug/{slug}", "site:read")]
    [Arguments("POST", "/api/v1/admin/blogs/", "site:create")]
    [Arguments("POST", "/api/v1/admin/blogs/{id:long}/translations", "site:create")]
    [Arguments("POST", "/api/v1/admin/blogs/{id:long}/ai-translate", "site:update")]
    [Arguments("PUT", "/api/v1/admin/blogs/{id:long}", "site:update")]
    [Arguments("POST", "/api/v1/admin/blogs/{id:long}/publish", "site:update")]
    [Arguments("POST", "/api/v1/admin/blogs/{id:long}/unpublish", "site:update")]
    [Arguments("DELETE", "/api/v1/admin/blogs/{id:long}", "site:delete")]
    [Arguments("DELETE", "/api/v1/admin/blogs/translation-groups/{translationGroupId:long}", "site:delete")]
    [Arguments("POST", "/api/v1/admin/blogs/translation-groups/{translationGroupId:long}/publish", "site:update")]
    [Arguments("POST", "/api/v1/admin/blogs/translation-groups/{translationGroupId:long}/unpublish", "site:update")]
    [Arguments("POST", "/api/v1/admin/blogs/import", "site:create")]
    [Arguments("GET", "/api/v1/admin/preview/blog-posts/{id:long}", "site:read")]
    [Arguments("POST", "/api/v1/admin/preview/blog-posts/render-fragment", "site:read")]
    public async Task PostsEndpoints_DeclareExpectedSitePolicy(
        string method,
        string route,
        string expectedPolicy)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAeroPostActor>(_ => null!);
        builder.Services.AddSingleton<ISiteContext>(_ => null!);
        builder.Services.AddSingleton<IQuerySession>(_ => null!);
        builder.Services.AddSingleton<IPostContentService>(_ => null!);
        builder.Services.AddSingleton<IAiContentTranslationService>(_ => null!);
        builder.Services.AddSingleton<IAuditService>(_ => null!);
        builder.Services.AddSingleton<IPostImportService>(_ => null!);
        await using var app = builder.Build();
        app.MapBlogApi();

        var endpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(candidate =>
                string.Equals(candidate.RoutePattern.RawText, route, StringComparison.Ordinal)
                && candidate.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Contains(method) == true);

        var policies = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
            .Select(data => data.Policy)
            .ToList();

        await Assert.That(policies).Contains(expectedPolicy);
    }

    [Test]
    public async Task DraftRazorRouteAloneRequiresSiteReadPermission()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLogging();
        new PostsModule().ConfigureServices(builder.Services);

        await using var app = builder.Build();
        app.MapRazorPages();
        await app.StartAsync();

        var pageEndpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<PageActionDescriptor>()?.AreaName == "Blog")
            .ToList();

        var preview = pageEndpoints.Single(endpoint => string.Equals(
            endpoint.RoutePattern.RawText?.TrimStart('/'),
            "_cms/preview/blog/drafts/{draftId:long}",
            StringComparison.OrdinalIgnoreCase));
        await Assert.That(preview.Metadata.GetOrderedMetadata<IAuthorizeData>()
            .Any(data => string.Equals(data.Policy, "site:read", StringComparison.Ordinal))).IsTrue();

        var publicRoutes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "blog",
            "{culture}/blog",
            "blog/{slug}",
            "{culture}/blog/{slug}"
        };
        var publicSelectors = pageEndpoints
            .Where(endpoint =>
                endpoint.RoutePattern.RawText is { } route
                && publicRoutes.Contains(route.TrimStart('/')))
            .ToList();

        await Assert.That(publicSelectors.Count).IsEqualTo(publicRoutes.Count);
        await Assert.That(publicSelectors.All(endpoint =>
            endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count == 0)).IsTrue();
    }
}
