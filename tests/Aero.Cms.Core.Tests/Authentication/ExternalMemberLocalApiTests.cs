using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Modules.Identity;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Aero.Cms.Core.Tests.Authentication;

public sealed class ExternalMemberLocalApiTests
{
    [Test]
    public async Task Local_browser_forms_require_antiforgery_while_token_endpoint_is_anonymous_json_without_antiforgery()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(Substitute.For<ISiteContext>());
        builder.Services.AddSingleton(Substitute.For<ILocalExternalMemberAuthenticationService>());
        builder.Services.AddSingleton(new ExternalMemberCookieIssuer(
            Substitute.For<IExternalMemberSessionRevocationService>(),
            TimeProvider.System));
        await using var app = builder.Build();
        app.MapExternalMemberLocalApi();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText is
                "/api/v1/member/local/activate" or
                "/api/v1/member/local/login" or
                "/api/v1/member/local/reset")
            .ToArray();

        await Assert.That(endpoints.Length).IsEqualTo(3);
        await Assert.That(endpoints.Select(endpoint => endpoint.RoutePattern.RawText!))
            .IsEquivalentTo([
                "/api/v1/member/local/activate",
                "/api/v1/member/local/login",
                "/api/v1/member/local/reset"
            ]);
        foreach (var endpoint in endpoints)
        {
            await Assert.That(endpoint.Metadata.GetMetadata<IAntiforgeryMetadata>()?.RequiresValidation).IsTrue();
            await Assert.That(endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods).Contains("POST");
        }

        var tokenEndpoint = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(endpoint => endpoint.RoutePattern.RawText == "/api/v1/member/local/token");
        await Assert.That(tokenEndpoint.Metadata.GetMetadata<IAntiforgeryMetadata>()?.RequiresValidation).IsFalse();
        await Assert.That(tokenEndpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.IAllowAnonymous>()).IsNotNull();
        await Assert.That(tokenEndpoint.Metadata.GetMetadata<IAcceptsMetadata>()?.ContentTypes).Contains("application/json");
    }

    [Test]
    public async Task Failed_cookie_issue_compensates_with_committed_receipt_scope()
    {
        var authentication = Substitute.For<IAuthenticationService>();
        authentication.SignInAsync(Arg.Any<HttpContext>(), Arg.Any<string>(),
                Arg.Any<System.Security.Claims.ClaimsPrincipal>(), Arg.Any<AuthenticationProperties?>())
            .Returns(Task.FromException(new InvalidOperationException("cookie failure")));
        authentication.SignOutAsync(Arg.Any<HttpContext>(), Arg.Any<string>(), Arg.Any<AuthenticationProperties?>())
            .Returns(Task.CompletedTask);
        using var services = new ServiceCollection().AddSingleton(authentication).BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        var revocation = Substitute.For<IExternalMemberSessionRevocationService>();
        revocation.RevokeAsync(Arg.Any<ExternalMemberSessionRevocationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Prelude.Ok<ExternalMemberSessionRevocationReceipt, AeroError>(new(41, 42, "local_identity", null)));
        var issuer = new ExternalMemberCookieIssuer(revocation, TimeProvider.System);
        var receipt = new ExternalMemberIssuanceReceipt(11, 12, 13, 41, 42, "local_identity", 1,
            DateTimeOffset.UtcNow.AddHours(1), "/shop");

        var issued = await issuer.TryIssueAsync(context, receipt);

        await Assert.That(issued).IsFalse();
        await revocation.Received(1).RevokeAsync(
            Arg.Is<ExternalMemberSessionRevocationRequest>(request =>
                request.TenantId == 41 && request.SiteId == 42 && request.ExternalMemberId == 11 &&
                request.ExternalMemberSessionId == 13),
            CancellationToken.None);
    }
}
