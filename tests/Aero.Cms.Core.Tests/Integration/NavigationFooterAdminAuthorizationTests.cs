using Aero.Cms.Modules.Footer.Areas.Api.v1;
using Aero.Cms.Modules.Navigation.Areas.Api.v1;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Core.Tests.Integration;

public sealed class NavigationFooterAdminAuthorizationTests
{
    [Test]
    public async Task Admin_surfaces_have_exact_twelve_route_permission_matrices_without_compatibility_put()
    {
        var builder = WebApplication.CreateBuilder();
        await using var app = builder.Build();
        app.MapNavigationAdminApi();
        app.MapFooterAdminApi();
        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();

        foreach (var module in new[] { "navigations", "footers" })
        {
            var prefix = $"/api/v1/admin/{module}";
            var moduleEndpoints = endpoints
                .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith(prefix, StringComparison.Ordinal) == true)
                .ToList();
            await Assert.That(moduleEndpoints.Count).IsEqualTo(12);
            await Assert.That(moduleEndpoints.Any(endpoint =>
                endpoint.RoutePattern.RawText == $"{prefix}/{{id:long}}" &&
                endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods.Contains("PUT"))).IsFalse();

            AssertPolicy(moduleEndpoints, "GET", $"{prefix}/", "site:read");
            AssertPolicy(moduleEndpoints, "GET", $"{prefix}/{{id:long}}", "site:read");
            AssertPolicy(moduleEndpoints, "GET", $"{prefix}/details/{{id:long}}", "site:read");
            AssertPolicy(moduleEndpoints, "GET", $"{prefix}/{{id:long}}/translations", "site:read");
            AssertPolicy(moduleEndpoints, "GET", $"{prefix}/{{id:long}}/events", "site:read");
            AssertPolicy(moduleEndpoints, "POST", $"{prefix}/", "site:create");
            AssertPolicy(moduleEndpoints, "POST", $"{prefix}/{{id:long}}/translations", "site:create");
            AssertPolicy(moduleEndpoints, "PUT", $"{prefix}/{{id:long}}/draft", "site:update");
            AssertPolicy(moduleEndpoints, "PUT", $"{prefix}/{{id:long}}/publish", "site:update");
            AssertPolicy(moduleEndpoints, "PUT", $"{prefix}/{{id:long}}/default", "site:update");
            AssertPolicy(moduleEndpoints, "DELETE", $"{prefix}/{{id:long}}", "site:delete");

            var ai = moduleEndpoints.Single(endpoint =>
                endpoint.RoutePattern.RawText == $"{prefix}/{{id:long}}/ai-translate");
            var aiPolicies = ai.Metadata.GetOrderedMetadata<IAuthorizeData>()
                .Select(data => data.Policy)
                .Where(policy => policy is not null)
                .ToList();
            await Assert.That(aiPolicies).Contains("site:create");
            await Assert.That(aiPolicies).Contains("site:update");
        }
    }

    private static void AssertPolicy(
        IReadOnlyList<RouteEndpoint> endpoints,
        string method,
        string route,
        string policy)
    {
        var endpoint = endpoints.Single(candidate =>
            candidate.RoutePattern.RawText == route &&
            candidate.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods.Contains(method));
        var policies = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
            .Select(data => data.Policy);
        if (!policies.Contains(policy))
        {
            throw new InvalidOperationException(
                $"Expected {method} {route} to require {policy}.");
        }
    }
}
