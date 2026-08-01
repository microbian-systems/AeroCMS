using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Modules.AiAssistant;
using Aero.Cms.Modules.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Shouldly;

namespace Aero.Cms.Core.Tests.Ai;

public sealed class SiteAssistantBoundaryTests
{
    [Test]
    public async Task Public_and_member_assistant_routes_have_distinct_auth_memory_and_rate_boundaries()
    {
        var builder = WebApplication.CreateBuilder();
        await using var app = builder.Build();
        app.MapAeroSiteAssistantEndpoints();
        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToArray();

        var publicEndpoints = endpoints
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith(
                "/api/v1/ai/",
                StringComparison.Ordinal) == true)
            .ToArray();
        var memberEndpoints = endpoints
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith(
                "/api/v1/member/assistant",
                StringComparison.Ordinal) == true)
            .ToArray();

        publicEndpoints.Length.ShouldBe(3);
        memberEndpoints.Length.ShouldBe(5);
        foreach (var endpoint in publicEndpoints)
        {
            endpoint.Metadata.GetMetadata<IAllowAnonymous>().ShouldNotBeNull();
            endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count.ShouldBe(0);
        }
        publicEndpoints.Single(endpoint =>
                endpoint.RoutePattern.RawText!.EndsWith("/complete", StringComparison.Ordinal))
            .Metadata.GetMetadata<EnableRateLimitingAttribute>()!
            .PolicyName.ShouldBe(AeroRateLimitPolicyNames.AiPublic);
        publicEndpoints.Single(endpoint =>
                endpoint.RoutePattern.RawText!.EndsWith("/stream", StringComparison.Ordinal))
            .Metadata.GetMetadata<EnableRateLimitingAttribute>()!
            .PolicyName.ShouldBe(AeroRateLimitPolicyNames.AiStream);
        publicEndpoints.Single(endpoint =>
                endpoint.RoutePattern.RawText!.EndsWith("/search", StringComparison.Ordinal))
            .Metadata.GetMetadata<EnableRateLimitingAttribute>()!
            .PolicyName.ShouldBe(AeroRateLimitPolicyNames.AiPublic);

        foreach (var endpoint in memberEndpoints)
        {
            var policies = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
                .Select(item => item.Policy)
                .ToArray();
            policies.ShouldContain(ExternalMemberAuthenticationDefaults.Policy);
            policies.ShouldContain(ExternalMemberAuthenticationDefaults.SitePolicy);
        }

        memberEndpoints.Single(endpoint =>
                endpoint.RoutePattern.RawText!.EndsWith("/stream", StringComparison.Ordinal))
            .Metadata.GetMetadata<EnableRateLimitingAttribute>()!
            .PolicyName.ShouldBe(AeroRateLimitPolicyNames.AiStream);
        memberEndpoints
            .Where(endpoint => !endpoint.RoutePattern.RawText!.EndsWith("/stream", StringComparison.Ordinal))
            .ShouldAllBe(endpoint =>
                endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>()!.PolicyName ==
                AeroRateLimitPolicyNames.AiMember);

        foreach (var endpoint in memberEndpoints.Where(endpoint =>
                     endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods
                         .Any(method => method is "POST" or "DELETE")))
        {
            var antiforgery = endpoint.Metadata.GetMetadata<IAntiforgeryMetadata>();
            antiforgery.ShouldNotBeNull();
            antiforgery.RequiresValidation.ShouldBeTrue();
        }
    }
}
