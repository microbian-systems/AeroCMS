using Aero.Cms.Modules.Content.Areas.Api.v1;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Core.Tests.Content;

public sealed class ContentApiAuthorizationTests
{
    [Test]
    public async Task Admin_content_endpoints_declare_exact_site_permission_matrix()
    {
        var builder = WebApplication.CreateBuilder();
        await using var app = builder.Build();
        app.MapContentTypesApi();
        app.MapContentItemsApi();
        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(x => x.Endpoints).OfType<RouteEndpoint>().ToList();
        await Assert.That(endpoints.Count).IsEqualTo(15);
        var expected = new (string Method, string Route, string Policy)[]
        {
            ("GET", "/api/v1/admin/content-types/", "site:read"),
            ("GET", "/api/v1/admin/content-types/{alias}", "site:read"),
            ("POST", "/api/v1/admin/content-types/", "site:create"),
            ("PUT", "/api/v1/admin/content-types/{alias}", "site:update"),
            ("DELETE", "/api/v1/admin/content-types/{alias}", "site:delete"),
            ("GET", "/api/v1/admin/content-items/", "site:read"),
            ("GET", "/api/v1/admin/content-items/{alias}/{id:long}", "site:read"),
            ("GET", "/api/v1/admin/content-items/{alias}/reference-options", "site:read"),
            ("POST", "/api/v1/admin/content-items/{alias}", "site:create"),
            ("PUT", "/api/v1/admin/content-items/{alias}/{id:long}", "site:update"),
            ("DELETE", "/api/v1/admin/content-items/{alias}/{id:long}", "site:delete"),
            ("POST", "/api/v1/admin/content-items/{alias}/{id:long}/publish", "site:update"),
            ("POST", "/api/v1/admin/content-items/{alias}/{id:long}/unpublish", "site:update"),
            ("GET", "/api/v1/admin/content-items/{alias}/{id:long}/translations", "site:read"),
            ("POST", "/api/v1/admin/content-items/{alias}/{id:long}/translations", "site:create")
        };
        foreach (var item in expected)
        {
            var endpoint = endpoints.Single(x => x.RoutePattern.RawText == item.Route &&
                x.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods.Contains(item.Method));
            await Assert.That(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(x => x.Policy)).Contains(item.Policy);
        }
    }
}
