using Aero.Cms.Modules.Content.Areas.Api.v1;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Shouldly;

namespace Aero.Cms.Core.Tests.Content;

public sealed class ContentViewsApiAuthorizationTests
{
    [Test]
    public async Task Content_view_endpoints_declare_read_and_update_site_policies()
    {
        var builder = WebApplication.CreateBuilder();
        await using var app = builder.Build();
        app.MapContentViewsApi();
        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToArray();
        var expected = new (string Method, string Route, string Policy, bool RequiresAdmin)[]
        {
            ("GET", "/api/v1/admin/content-views/shapes", "site:read", true),
            ("GET", "/api/v1/admin/content-views/{alias}", "site:read", true),
            ("PUT", "/api/v1/admin/content-views/{alias}/draft", "site:update", true),
            ("POST", "/api/v1/admin/content-views/{alias}/preview", "site:read", true),
            ("POST", "/api/v1/admin/content-views/{alias}/publish", "site:update", true),
            ("POST", "/api/v1/admin/content-views/{alias}/cache/invalidate", "site:update", true),
            // Relationship metadata exposes physical schema topology and is intentionally admin-only.
            ("GET", "/api/v1/admin/content-views/{alias}/relationships", "site:read", true),
            ("PUT", "/api/v1/admin/content-views/{alias}/relationships/{relationshipAlias}/draft", "site:update", true),
            ("POST", "/api/v1/admin/content-views/{alias}/relationships/{relationshipId:long}/ddl/preview", "site:update", true),
            ("POST", "/api/v1/admin/content-views/{alias}/relationships/{relationshipId:long}/ddl/apply", "site:update", true),
            ("GET", "/api/v1/admin/content-views/entries", "site:read", false),
            ("GET", "/api/v1/admin/content-views/entries/{provider}", "site:read", false),
            ("GET", "/api/v1/admin/content-views/entries/{provider}/{stableId}", "site:read", false)
        };

        endpoints.Length.ShouldBe(expected.Length);
        foreach (var item in expected)
        {
            var endpoint = endpoints.Single(candidate =>
                candidate.RoutePattern.RawText == item.Route
                && candidate.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods.Contains(item.Method));
            endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(data => data.Policy)
                .ShouldContain(item.Policy);
            endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(data => data.Policy == "AeroAdmin")
                .ShouldBe(item.RequiresAdmin);
        }
    }
}
