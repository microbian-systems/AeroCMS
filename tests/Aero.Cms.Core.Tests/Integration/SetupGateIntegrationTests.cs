using Aero.Cms.Modules.Setup;
using Aero.Cms.Modules.Blog;
using Aero.Cms.Modules.Pages;
using Aero.Cms.ServiceDefaults;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Aero.Cms.Abstractions.Blocks;
using NSubstitute;
using System.Text.RegularExpressions;
using ZiggyCreatures.Caching.Fusion;

namespace Aero.Cms.Core.Tests.Integration;

public class SetupGateIntegrationTests
{
    [Test]
    public async Task Fresh_start_requests_redirect_to_setup_while_allowlisted_routes_stay_reachable()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        var setupResponse = await client.GetAsync("/setup");
        var homeResponse = await client.GetAsync("/");
        var adminResponse = await client.GetAsync("/admin");
        var healthResponse = await client.GetAsync("/health");
        var aliveResponse = await client.GetAsync("/alive");
        var frameworkResponse = await client.GetAsync("/_framework/test.js");
        var errorResponse = await client.GetAsync("/error");

        setupResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        homeResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.TemporaryRedirect);
        homeResponse.Headers.Location.Should().NotBeNull();
        homeResponse.Headers.Location!.OriginalString.Should().Be(SetupPathAllowlist.SetupPath);
        adminResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.TemporaryRedirect);
        adminResponse.Headers.Location.Should().NotBeNull();
        adminResponse.Headers.Location!.OriginalString.Should().Be(SetupPathAllowlist.SetupPath);
        healthResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        aliveResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        frameworkResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        errorResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
    }

    [Test]
    public async Task Setup_page_renders_the_setup_wizard_surface()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/setup");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        html.Should().Contain("Aero CMS Setup");
        html.Should().Contain("Administrator access");
        html.Should().Contain("Starter site metadata");
        html.Should().Contain("name=\"Input.AdminUserName\"");
        html.Should().Contain("name=\"Input.AdminEmail\"");
        html.Should().Contain("name=\"Input.Password\"");
        html.Should().Contain("name=\"Input.ConfirmPassword\"");
        html.Should().Contain("name=\"Input.SiteName\"");
        html.Should().Contain("name=\"Input.HomepageTitle\"");
        html.Should().Contain("name=\"Input.BlogName\"");
    }

    [Test]
    public async Task Setup_submit_reaches_the_running_page_model_and_redirects_after_bootstrap()
    {
        var harness = new InMemoryCmsDocumentSessionHarness();
        var bootstrapper = Substitute.For<ISetupIdentityBootstrapper>();
        bootstrapper.BootstrapAsync(Arg.Any<SetupIdentityBootstrapRequest>(), Arg.Any<CancellationToken>())
            .Returns(new SetupIdentityBootstrapResult { CreatedAdmin = true });

        await using var app = await CreateAppAsync(harness, bootstrapper: bootstrapper);
        using var client = app.GetTestClient();

        using var getResponse = await client.GetAsync("/setup?returnUrl=%2Fadmin");
        var html = await getResponse.Content.ReadAsStringAsync();
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"(?<token>[^\"]+)\"")
            .Groups["token"]
            .Value;
        var antiforgeryCookie = getResponse.Headers.GetValues("Set-Cookie")
            .Select(value => value.Split(';', 2)[0])
            .First(cookie => cookie.StartsWith(".AspNetCore.Antiforgery.", StringComparison.Ordinal));

        token.Should().NotBeNullOrWhiteSpace();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/setup?returnUrl=%2Fadmin")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Input.AdminUserName"] = "admin.user",
                ["Input.AdminEmail"] = "admin@example.com",
                ["Input.Password"] = "CorrectHorseBattery1!",
                ["Input.ConfirmPassword"] = "CorrectHorseBattery1!",
                ["Input.SiteName"] = "Aero CMS",
                ["Input.HomepageTitle"] = "Welcome to Aero CMS",
                ["Input.BlogName"] = "Field Notes"
            })
        };
        request.Headers.Add("Cookie", antiforgeryCookie);

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Redirect);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.OriginalString.Should().Be("/admin");
        harness.SetupStates.Should().ContainKey(SetupStateDocument.FixedId);
        harness.SetupStates[SetupStateDocument.FixedId].IsComplete.Should().BeTrue();
        harness.Pages.Values.Any(p => p.Slug == "/").Should().BeTrue();
        harness.Pages.Values.Any(p => p.Slug == "blog").Should().BeTrue();
        harness.BlogPosts.Should().HaveCount(3);
        await bootstrapper.Received(1)
            .BootstrapAsync(Arg.Is<SetupIdentityBootstrapRequest>(request =>
                request.AdminUserName == "admin.user" &&
                request.AdminEmail == "admin@example.com"), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Fresh_start_non_get_requests_are_blocked_without_redirecting()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        using var response = await client.PostAsync("/admin", new StringContent(string.Empty));

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
        response.Headers.Location.Should().BeNull();
    }

    [Test]
    public async Task Marten_setup_state_store_loads_the_fixed_singleton_id()
    {
        var harness = new InMemoryCmsDocumentSessionHarness();
        harness.Session.Store(new SetupStateDocument
        {
            Id = SetupStateDocument.FixedId,
            IsComplete = true
        });

        var store = new MartenSetupStateStore(harness.Session);

        var state = await store.LoadAsync();

        state.Should().NotBeNull();
        state!.Id.Should().Be(SetupStateDocument.FixedId);
        state.IsComplete.Should().BeTrue();
        await harness.Session.Received(1).LoadAsync<SetupStateDocument>(SetupStateDocument.FixedId, Arg.Any<CancellationToken>());
    }

    private static async Task<WebApplication> CreateAppAsync(
        InMemoryCmsDocumentSessionHarness? harness = null,
        ISetupIdentityBootstrapper? bootstrapper = null,
        bool enableAntiforgery = true)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development,
            ApplicationName = typeof(SetupModule).Assembly.GetName().Name
        });

        builder.WebHost.UseTestServer();
        builder.Services.AddLogging();
        builder.Services.AddControllersWithViews();
        builder.Services.AddAuthentication();
        builder.Services.AddAuthorization();
        builder.Services.AddHealthChecks();
        builder.Services.AddRazorPages()
            .AddApplicationPart(typeof(SetupModule).Assembly);

        harness ??= new InMemoryCmsDocumentSessionHarness();

        builder.Services.AddScoped(_ => harness.Session);
        builder.Services.AddScoped<IDocumentSession>(_ => harness.Session);
        builder.Services.AddScoped<IQuerySession>(_ => harness.Session);
        builder.Services.AddSingleton(Substitute.For<IBlockService>());
        builder.Services.AddSingleton(Substitute.For<IFusionCache>());

        bootstrapper ??= Substitute.For<ISetupIdentityBootstrapper>();
        bootstrapper.BootstrapAsync(Arg.Any<SetupIdentityBootstrapRequest>(), Arg.Any<CancellationToken>())
            .Returns(new SetupIdentityBootstrapResult { CreatedAdmin = true });

        var module = new SetupModule();
        module.ConfigureServices(builder.Services, new ConfigurationBuilder().Build(), builder.Environment);
        var pagesModule = new PagesModule();
        pagesModule.ConfigureServices(builder.Services, new ConfigurationBuilder().Build(), builder.Environment);
        var blogModule = new BlogModule();
        blogModule.ConfigureServices(builder.Services, new ConfigurationBuilder().Build(), builder.Environment);
        builder.Services.RemoveAll<ISetupIdentityBootstrapper>();
        builder.Services.AddSingleton(bootstrapper);
        var healthModule = new Aero.Cms.Modules.Health.HealthModule();
        healthModule.ConfigureServices(builder.Services, new ConfigurationBuilder().Build(), builder.Environment);

        var app = builder.Build();

        app.MapDefaultEndpoints();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseCmsSetupGate();
        if (enableAntiforgery)
        {
            app.UseAntiforgery();
        }

        app.MapRazorPages();
        app.MapGet("/", () => Results.Ok("home"));
        app.MapGet("/admin", () => Results.Ok("admin"));
        app.MapGet("/error", () => Results.Ok("error"));
        app.MapGet("/not-found", () => Results.Ok("not-found"));
        app.MapGet("/_framework/test.js", () => Results.Text("console.log('ok');", "application/javascript"));

        await healthModule.RunAsync(app);

        await app.StartAsync();

        return app;
    }
}
