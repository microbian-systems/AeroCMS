using System.Net;
using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Modules.Identity;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using NSubstitute;

namespace Aero.Cms.Core.Tests.Authentication;

public sealed class ManagerLoginStartupRegressionTests
{
    [Test]
    public async Task Authentication_config_returns_complete_server_resolution_without_caching()
    {
        var expected = new ManagerAuthenticationModeResolution(
            ManagerIdentityProviders.WorkOs,
            ManagerIdentityProviders.WorkOs,
            ManagerAuthenticationModeStatuses.Remote,
            42);
        var resolver = Substitute.For<IManagerAuthenticationModeResolver>();
        resolver.ResolveAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Prelude.Ok<ManagerAuthenticationModeResolution, AeroError>(expected)));
        var context = new DefaultHttpContext();

        var result = await IdentityApi.GetAuthenticationConfigAsync(context, resolver, CancellationToken.None);

        var valueResult = result as IValueHttpResult;
        await Assert.That(valueResult).IsNotNull();
        await Assert.That(valueResult!.Value).IsEqualTo(expected);
        await Assert.That(context.Response.Headers.CacheControl.ToString()).IsEqualTo("no-store");
    }

    [Test]
    public async Task Api_cookie_login_challenge_returns_unauthorized_without_redirect()
    {
        var options = new CookieAuthenticationOptions();
        var sut = new ManagerApiCookieRedirectPostConfigureOptions();
        sut.PostConfigure(IdentityConstants.ApplicationScheme, options);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/v1/admin/auth/me";
        var context = CreateRedirectContext(httpContext, options, "/manager/login");

        await options.Events.OnRedirectToLogin(context);

        await Assert.That(httpContext.Response.StatusCode).IsEqualTo(StatusCodes.Status401Unauthorized);
        await Assert.That(httpContext.Response.Headers.Location.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Api_cookie_access_denied_returns_forbidden_without_redirect()
    {
        var options = new CookieAuthenticationOptions();
        var sut = new ManagerApiCookieRedirectPostConfigureOptions();
        sut.PostConfigure(IdentityConstants.ApplicationScheme, options);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/v1/admin/users";
        var context = CreateRedirectContext(httpContext, options, "/account/denied");

        await options.Events.OnRedirectToAccessDenied(context);

        await Assert.That(httpContext.Response.StatusCode).IsEqualTo(StatusCodes.Status403Forbidden);
        await Assert.That(httpContext.Response.Headers.Location.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Non_api_cookie_challenge_preserves_existing_redirect_handler()
    {
        var existingHandlerCalled = false;
        var options = new CookieAuthenticationOptions();
        options.Events.OnRedirectToLogin = context =>
        {
            existingHandlerCalled = true;
            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
        var sut = new ManagerApiCookieRedirectPostConfigureOptions();
        sut.PostConfigure(IdentityConstants.ApplicationScheme, options);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/manager";
        var context = CreateRedirectContext(httpContext, options, "/manager/login");

        await options.Events.OnRedirectToLogin(context);

        await Assert.That(existingHandlerCalled).IsTrue();
        await Assert.That(httpContext.Response.StatusCode).IsEqualTo(StatusCodes.Status302Found);
        await Assert.That(httpContext.Response.Headers.Location.ToString()).IsEqualTo("/manager/login");
    }

    private static RedirectContext<CookieAuthenticationOptions> CreateRedirectContext(
        HttpContext httpContext,
        CookieAuthenticationOptions options,
        string redirectUri)
        => new(
            httpContext,
            new AuthenticationScheme(
                IdentityConstants.ApplicationScheme,
                IdentityConstants.ApplicationScheme,
                typeof(CookieAuthenticationHandler)),
            options,
            new AuthenticationProperties(),
            redirectUri);
}
