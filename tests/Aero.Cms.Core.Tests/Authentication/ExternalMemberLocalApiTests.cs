using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Modules.Identity;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Aero.Cms.Core.Tests.Authentication;

public sealed class ExternalMemberLocalApiTests
{
    [Test]
    public async Task All_local_browser_forms_require_antiforgery_validation()
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
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith("/api/v1/member/local/", StringComparison.Ordinal) == true)
            .ToArray();

        await Assert.That(endpoints.Length).IsEqualTo(3);
        foreach (var endpoint in endpoints)
        {
            await Assert.That(endpoint.Metadata.GetMetadata<IAntiforgeryMetadata>()?.RequiresValidation).IsTrue();
            await Assert.That(endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods).Contains("POST");
        }
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
