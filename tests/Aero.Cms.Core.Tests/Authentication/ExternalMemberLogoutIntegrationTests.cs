using System.Net;
using System.Security.Claims;
using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Identity;
using AeroDB.Sable;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Aero.Cms.Core.Tests.Authentication;

public sealed class ExternalMemberLogoutIntegrationTests
{
    private const long MemberId = 101;
    private const long SessionId = 202;

    [Test]
    public async Task External_cookie_cannot_authenticate_default_manager_endpoints_and_internal_cookie_cannot_authenticate_member_endpoints()
    {
        var querySession = Substitute.For<IQuerySession>();
        var documentSession = Substitute.For<IDocumentSession>();
        await using var app = await CreateAppAsync(querySession, documentSession);
        using var client = app.GetTestClient();
        var externalCookie = await SignInAsync(client, "/test/signin/external", ".AeroCms.Member");
        var internalCookie = await SignInAsync(client, "/test/signin/internal", ".AeroCms.Auth");

        using var managerRequest = new HttpRequestMessage(HttpMethod.Get, "/manager-probe");
        managerRequest.Headers.Add("Cookie", externalCookie);
        using var managerResponse = await client.SendAsync(managerRequest);

        using var memberRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/member/logout");
        memberRequest.Headers.Add("Cookie", internalCookie);
        using var memberResponse = await client.SendAsync(memberRequest);

        await Assert.That(managerResponse.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(memberResponse.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Logout_revokes_owned_session_clears_member_cookie_and_preserves_internal_cookie()
    {
        var externalSession = ActiveSession();
        var querySession = Substitute.For<IQuerySession>();
        querySession.LoadAsync<ExternalMemberSession>(SessionId, Arg.Any<CancellationToken>())
            .Returns(externalSession);
        var documentSession = Substitute.For<IDocumentSession>();
        documentSession.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
        await using var app = await CreateAppAsync(querySession, documentSession);
        using var client = app.GetTestClient();
        var externalCookie = await SignInAsync(client, "/test/signin/external", ".AeroCms.Member");
        var internalCookie = await SignInAsync(client, "/test/signin/internal", ".AeroCms.Auth");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/member/logout");
        request.Headers.Add("Cookie", $"{externalCookie}; {internalCookie}");
        using var response = await client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(externalSession.RevokedAt).IsNotNull();
        await documentSession.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await AssertMemberCookieOnlyIsDeletedAsync(response);
    }

    [Test]
    public async Task Logout_still_clears_member_cookie_and_reports_failure_when_revocation_save_fails()
    {
        var querySession = Substitute.For<IQuerySession>();
        querySession.LoadAsync<ExternalMemberSession>(SessionId, Arg.Any<CancellationToken>())
            .Returns(ActiveSession());
        var documentSession = Substitute.For<IDocumentSession>();
        documentSession.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<int>(new InvalidOperationException("store unavailable")));
        await using var app = await CreateAppAsync(querySession, documentSession);
        using var client = app.GetTestClient();
        var externalCookie = await SignInAsync(client, "/test/signin/external", ".AeroCms.Member");
        var internalCookie = await SignInAsync(client, "/test/signin/internal", ".AeroCms.Auth");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/member/logout");
        request.Headers.Add("Cookie", $"{externalCookie}; {internalCookie}");
        using var response = await client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.InternalServerError);
        await AssertMemberCookieOnlyIsDeletedAsync(response);
    }

    [Test]
    public async Task Logout_does_not_revoke_a_session_owned_by_another_member()
    {
        var foreignSession = ActiveSession();
        foreignSession.ExternalMemberId = MemberId + 1;
        var querySession = Substitute.For<IQuerySession>();
        querySession.LoadAsync<ExternalMemberSession>(SessionId, Arg.Any<CancellationToken>())
            .Returns(foreignSession);
        var documentSession = Substitute.For<IDocumentSession>();
        await using var app = await CreateAppAsync(querySession, documentSession);
        using var client = app.GetTestClient();
        var externalCookie = await SignInAsync(client, "/test/signin/external", ".AeroCms.Member");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/member/logout");
        request.Headers.Add("Cookie", externalCookie);
        using var response = await client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(foreignSession.RevokedAt).IsNull();
        await documentSession.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        await AssertMemberCookieOnlyIsDeletedAsync(response);
    }

    private static async Task<WebApplication> CreateAppAsync(
        IQuerySession querySession,
        IDocumentSession documentSession)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLogging();
        builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = IdentityConstants.ApplicationScheme;
                options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
                options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
            })
            .AddCookie(IdentityConstants.ApplicationScheme, options =>
            {
                options.Cookie.Name = ".AeroCms.Auth";
                ConfigureApiRedirects(options);
            })
            .AddCookie(ExternalMemberAuthenticationDefaults.Scheme, options =>
            {
                options.Cookie.Name = ".AeroCms.Member";
                ConfigureApiRedirects(options);
            });
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(ExternalMemberAuthenticationDefaults.Policy, policy =>
            {
                policy.AddAuthenticationSchemes(ExternalMemberAuthenticationDefaults.Scheme);
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(context => ExternalMemberPrincipal.TryRead(context.User, out _));
            });
            options.AddPolicy(ExternalMemberAuthenticationDefaults.SitePolicy, policy =>
            {
                policy.AddAuthenticationSchemes(ExternalMemberAuthenticationDefaults.Scheme);
                policy.RequireAuthenticatedUser();
            });
        });
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentPrincipal, CurrentPrincipal>();
        builder.Services.AddSingleton(querySession);
        builder.Services.AddSingleton(documentSession);

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/test/signin/external", async (HttpContext context) =>
        {
            await context.SignInAsync(
                ExternalMemberAuthenticationDefaults.Scheme,
                ExternalMemberPrincipal.Create(MemberId, "workos", SessionId, 3));
            return Results.NoContent();
        }).AllowAnonymous();
        app.MapGet("/test/signin/internal", async (HttpContext context) =>
        {
            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "909")],
                IdentityConstants.ApplicationScheme);
            await context.SignInAsync(
                IdentityConstants.ApplicationScheme,
                new ClaimsPrincipal(identity));
            return Results.NoContent();
        }).AllowAnonymous();
        app.MapGet("/manager-probe", () => Results.NoContent())
            .RequireAuthorization();
        app.MapExternalMemberApi();
        await app.StartAsync();
        return app;
    }

    private static void ConfigureApiRedirects(CookieAuthenticationOptions options)
    {
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    }

    private static async Task<string> SignInAsync(HttpClient client, string path, string cookieName)
    {
        using var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        return response.Headers.GetValues("Set-Cookie")
            .Select(value => value.Split(';', 2)[0])
            .Single(value => value.StartsWith($"{cookieName}=", StringComparison.Ordinal));
    }

    private static async Task AssertMemberCookieOnlyIsDeletedAsync(HttpResponseMessage response)
    {
        var setCookies = response.Headers.GetValues("Set-Cookie").ToArray();
        await Assert.That(setCookies.Any(value =>
            value.StartsWith(".AeroCms.Member=", StringComparison.Ordinal) &&
            value.Contains("expires=", StringComparison.OrdinalIgnoreCase))).IsTrue();
        await Assert.That(setCookies.Any(value =>
            value.StartsWith(".AeroCms.Auth=", StringComparison.Ordinal))).IsFalse();
    }

    private static ExternalMemberSession ActiveSession() => new()
    {
        Id = SessionId,
        ExternalMemberId = MemberId,
        AuthenticationProvider = "workos",
        SecurityVersion = 3,
        ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
    };
}
