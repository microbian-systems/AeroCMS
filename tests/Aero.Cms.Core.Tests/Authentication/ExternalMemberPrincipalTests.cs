using System.Security.Claims;
using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Modules.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Core.Tests.Authentication;

public sealed class ExternalMemberPrincipalTests
{
    [Test]
    public async Task Factory_creates_the_only_accepted_external_member_claim_shape()
    {
        var principal = ExternalMemberPrincipal.Create(101, "workos", 202, 3, "Taylor");

        var parsed = ExternalMemberPrincipal.TryRead(principal, out var claims);

        await Assert.That(parsed).IsTrue();
        await Assert.That(claims.MemberId).IsEqualTo(101);
        await Assert.That(claims.Provider).IsEqualTo("workos");
        await Assert.That(claims.SessionId).IsEqualTo(202);
        await Assert.That(claims.SecurityVersion).IsEqualTo(3);
    }

    [Test]
    [Arguments("role", "Admin")]
    [Arguments("roles", "Admin")]
    [Arguments("is_admin", "true")]
    [Arguments("permission", "site:read")]
    public async Task Privileged_or_manager_claims_are_rejected(string type, string value)
    {
        var principal = ExternalMemberPrincipal.Create(101, "entra_external_id", 202, 3);
        principal.AddIdentity(new ClaimsIdentity([new Claim(type, value)], ExternalMemberAuthenticationDefaults.Scheme));

        await Assert.That(ExternalMemberPrincipal.TryRead(principal, out _)).IsFalse();
    }

    [Test]
    public async Task Duplicate_claims_are_rejected_and_exact_local_identity_is_accepted()
    {
        var duplicate = ExternalMemberPrincipal.Create(101, "workos", 202, 3);
        duplicate.AddIdentity(new ClaimsIdentity([new Claim(ExternalMemberClaimTypes.SessionId, "303")], ExternalMemberAuthenticationDefaults.Scheme));
        var localIdentity = ExternalMemberPrincipal.Create(
            101, LocalExternalMemberAuthentication.Provider, 202, 3);

        await Assert.That(ExternalMemberPrincipal.TryRead(duplicate, out _)).IsFalse();
        await Assert.That(ExternalMemberPrincipal.TryRead(localIdentity, out var localClaims)).IsTrue();
        await Assert.That(localClaims.Provider).IsEqualTo(LocalExternalMemberAuthentication.Provider);
    }

    [Test]
    public async Task Identical_claims_on_an_internal_authentication_type_are_rejected()
    {
        var valid = ExternalMemberPrincipal.Create(101, "workos", 202, 3);
        var internalIdentity = new ClaimsIdentity(valid.Claims, "Identity.Application");

        await Assert.That(ExternalMemberPrincipal.TryRead(new ClaimsPrincipal(internalIdentity), out _)).IsFalse();
    }

    [Test]
    public async Task Member_routes_require_the_dedicated_external_member_policy()
    {
        var builder = WebApplication.CreateBuilder();
        await using var app = builder.Build();
        app.MapExternalMemberApi();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>().ToList();
        var me = endpoints.Single(endpoint => endpoint.RoutePattern.RawText == "/api/v1/member/me");
        var logout = endpoints.Single(endpoint => endpoint.RoutePattern.RawText == "/api/v1/member/logout");

        await Assert.That(me.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(data => data.Policy))
            .Contains(ExternalMemberAuthenticationDefaults.Policy);
        await Assert.That(me.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(data => data.Policy))
            .Contains(ExternalMemberAuthenticationDefaults.SitePolicy);
        await Assert.That(logout.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(data => data.Policy))
            .Contains(ExternalMemberAuthenticationDefaults.Policy);
        await Assert.That(logout.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(data => data.Policy))
            .Contains(ExternalMemberAuthenticationDefaults.SitePolicy);
        await Assert.That(logout.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods).Contains("POST");
    }

}
