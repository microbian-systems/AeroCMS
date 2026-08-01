using System.Security.Claims;
using Aero.Cms.Modules.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Core.Tests.Authentication;

public sealed class ManagerAuthenticationSchemeRoutingTests
{
    [Test]
    [Arguments(null)]
    [Arguments(".AeroCms.ManagerRecovery=stale-or-tampered")]
    public async Task Manager_challenge_always_uses_normal_login(string? cookie)
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();
        if (cookie is not null)
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", cookie);
        }

        using var response = await client.GetAsync("/challenge");

        await Assert.That(response.StatusCode).IsEqualTo(System.Net.HttpStatusCode.Redirect);
        await Assert.That(response.Headers.Location!.AbsolutePath).IsEqualTo("/manager/login");
    }

    [Test]
    public async Task Valid_recovery_cookie_authenticates_as_recovery_when_application_cookie_is_absent()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();
        var recoveryCookie = await IssueCookieAsync(client, "/issue/recovery");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", recoveryCookie);

        using var response = await client.GetAsync("/authenticate");

        await Assert.That(response.StatusCode).IsEqualTo(System.Net.HttpStatusCode.OK);
        await Assert.That(await response.Content.ReadAsStringAsync()).IsEqualTo("recovery");
    }

    [Test]
    public async Task Application_cookie_wins_authentication_when_both_manager_cookies_are_present()
    {
        await using var app = await CreateAppAsync();
        using var issuer = app.GetTestClient();
        var recoveryCookie = await IssueCookieAsync(issuer, "/issue/recovery");
        var applicationCookie = await IssueCookieAsync(issuer, "/issue/application");
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", $"{recoveryCookie}; {applicationCookie}");

        using var response = await client.GetAsync("/authenticate");

        await Assert.That(response.StatusCode).IsEqualTo(System.Net.HttpStatusCode.OK);
        await Assert.That(await response.Content.ReadAsStringAsync()).IsEqualTo("application");
    }

    private static async Task<WebApplication> CreateAppAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddAuthentication(ManagerRecoveryDefaults.ManagerScheme)
            .AddPolicyScheme(
                ManagerRecoveryDefaults.ManagerScheme,
                null,
                ManagerAuthenticationSchemeRouting.Configure)
            .AddCookie(IdentityConstants.ApplicationScheme, options =>
            {
                options.Cookie.Name = ".AeroCms.Auth";
                options.LoginPath = "/manager/login";
            })
            .AddCookie(ManagerRecoveryDefaults.Scheme, options =>
            {
                options.Cookie.Name = ManagerRecoveryDefaults.CookieName;
            });

        var app = builder.Build();
        app.UseAuthentication();
        app.MapGet("/challenge", async context => await context.ChallengeAsync(ManagerRecoveryDefaults.ManagerScheme));
        app.MapGet("/issue/recovery", async context =>
            await context.SignInAsync(ManagerRecoveryDefaults.Scheme, CreatePrincipal("recovery", ManagerRecoveryDefaults.Scheme)));
        app.MapGet("/issue/application", async context =>
            await context.SignInAsync(IdentityConstants.ApplicationScheme, CreatePrincipal("application", IdentityConstants.ApplicationScheme)));
        app.MapGet("/authenticate", async context =>
        {
            var result = await context.AuthenticateAsync(ManagerRecoveryDefaults.ManagerScheme);
            context.Response.StatusCode = result.Succeeded
                ? StatusCodes.Status200OK
                : StatusCodes.Status401Unauthorized;
            if (result.Succeeded)
            {
                await context.Response.WriteAsync(result.Principal!.FindFirstValue("route") ?? string.Empty);
            }
        });
        await app.StartAsync();
        return app;
    }

    private static ClaimsPrincipal CreatePrincipal(string route, string scheme)
        => new(new ClaimsIdentity([new Claim("route", route)], scheme));

    private static async Task<string> IssueCookieAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        await Assert.That(response.StatusCode).IsEqualTo(System.Net.HttpStatusCode.OK);
        return response.Headers.GetValues("Set-Cookie").Single().Split(';', 2)[0];
    }
}
