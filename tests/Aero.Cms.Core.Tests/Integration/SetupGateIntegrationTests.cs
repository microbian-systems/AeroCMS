using Aero.Cms.Modules.Setup;
using Aero.Cms.Modules.Posts;
using Aero.Cms.Modules.Pages;
using Aero.Cms.ServiceDefaults;
using AeroDB.Sable;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Core.Blocks;
using Aero.Cms.Core.Entities;
using Aero.Core.Http;
using Aero.Services.Images;
using NSubstitute;
using Shouldly;
using System.Text.RegularExpressions;
using Orleans;
using Wolverine;
using ZiggyCreatures.Caching.Fusion;

namespace Aero.Cms.Core.Tests.Integration;

public sealed class SetupGateIntegrationTests
{
    [Test]
    public async Task Fresh_start_requests_redirect_to_setup_while_allowlisted_routes_stay_reachable()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<SetupStateDocument>().WithSchema<PageDocument>()
            .WithSchema<PostDocument>().WithSchema<ContentSlugDocument>();
        await harness.InitializeAsync();
        await using var app = await CreateAppAsync(harness);
        using var client = app.GetTestClient();

        var setupResponse = await client.GetAsync("/setup");
        var homeResponse = await client.GetAsync("/");
        var adminResponse = await client.GetAsync("/admin");
        var healthResponse = await client.GetAsync("/health");
        var aliveResponse = await client.GetAsync("/alive");
        var frameworkResponse = await client.GetAsync("/_framework/test.js");
        var errorResponse = await client.GetAsync("/error");

        setupResponse.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        homeResponse.StatusCode.ShouldBe(System.Net.HttpStatusCode.TemporaryRedirect);
        homeResponse.Headers.Location.ShouldNotBeNull();
        homeResponse.Headers.Location!.OriginalString.ShouldBe(SetupPathAllowlist.SetupPath);
        adminResponse.StatusCode.ShouldBe(System.Net.HttpStatusCode.TemporaryRedirect);
        adminResponse.Headers.Location.ShouldNotBeNull();
        adminResponse.Headers.Location!.OriginalString.ShouldBe(SetupPathAllowlist.SetupPath);
        healthResponse.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        aliveResponse.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        frameworkResponse.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        errorResponse.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
    }

    [Test]
    public async Task Setup_page_renders_the_setup_wizard_surface()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<SetupStateDocument>().WithSchema<PageDocument>()
            .WithSchema<PostDocument>().WithSchema<ContentSlugDocument>();
        await harness.InitializeAsync();
        await using var app = await CreateAppAsync(harness);
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/setup");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        html.ShouldContain("Aero CMS Setup");
        html.ShouldContain("Administrator access");
        html.ShouldContain("Starter site metadata");
        html.ShouldContain("name=\"Input.AdminUserName\"");
        html.ShouldContain("name=\"Input.AdminEmail\"");
        html.ShouldContain("name=\"Input.Password\"");
        html.ShouldContain("name=\"Input.ConfirmPassword\"");
        html.ShouldContain("name=\"Input.SiteName\"");
        html.ShouldContain("name=\"Input.HomepageTitle\"");
        html.ShouldContain("name=\"Input.BlogName\"");
    }

    [Test]
    public async Task Setup_submit_reaches_the_running_page_model_and_redirects_after_bootstrap()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<SetupStateDocument>().WithSchema<PageDocument>()
            .WithSchema<PostDocument>().WithSchema<ContentSlugDocument>();
        await harness.InitializeAsync();

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

        token.ShouldNotBeNullOrWhiteSpace();

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

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.Redirect);
        response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location!.OriginalString.ShouldBe("/admin");

        // Verify setup state was persisted in the real DB
        var state = await harness.Session.LoadAsync<SetupStateDocument>(SetupStateDocument.FixedId);
        state.ShouldNotBeNull();
        state!.IsComplete.ShouldBeTrue();

        // Verify pages were created
        (await harness.Session.Query<PageDocument>().Where(p => p.Slug == "/").AnyAsync()).ShouldBeTrue();
        (await harness.Session.Query<PageDocument>().Where(p => p.Slug == "blog").AnyAsync()).ShouldBeTrue();

        // Verify blog posts were created
        var postCount = await harness.Session.Query<PostDocument>().CountAsync();
        postCount.ShouldBe(3);

        await bootstrapper.Received(1)
            .BootstrapAsync(Arg.Is<SetupIdentityBootstrapRequest>(request =>
                request.AdminUserName == "admin.user" &&
                request.AdminEmail == "admin@example.com"), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Fresh_start_non_get_requests_are_blocked_without_redirecting()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<SetupStateDocument>().WithSchema<PageDocument>()
            .WithSchema<PostDocument>().WithSchema<ContentSlugDocument>();
        await harness.InitializeAsync();
        await using var app = await CreateAppAsync(harness);
        using var client = app.GetTestClient();

        using var response = await client.PostAsync("/admin", new StringContent(string.Empty));

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.NotFound);
        response.Headers.Location.ShouldBeNull();
    }

    [Test]
    public async Task Marten_setup_state_store_loads_the_fixed_singleton_id()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<SetupStateDocument>().WithSchema<PageDocument>()
            .WithSchema<PostDocument>().WithSchema<ContentSlugDocument>();
        await harness.InitializeAsync();

        harness.Session.Store(new SetupStateDocument
        {
            Id = SetupStateDocument.FixedId,
            IsComplete = true
        });
        await harness.Session.SaveChangesAsync();

        var store = new AeroSetupStateStore(harness.Session);

        var state = await store.LoadAsync();

        state.ShouldNotBeNull();
        state!.Id.ShouldBe(SetupStateDocument.FixedId);
        state.IsComplete.ShouldBeTrue();
    }

    private static async Task<WebApplication> CreateAppAsync(
        SableTestHarness? harness = null,
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

        harness ??= new SableTestHarness()
            .WithSchema<SetupStateDocument>().WithSchema<PageDocument>()
            .WithSchema<PostDocument>().WithSchema<ContentSlugDocument>();
        await harness.InitializeAsync();

        builder.Services.AddScoped(_ => harness.Session);
        builder.Services.AddScoped<IDocumentSession>(_ => harness.Session);
        builder.Services.AddScoped<IQuerySession>(_ => harness.Session);
        builder.Services.AddSingleton(Substitute.For<IBlockService>());
        builder.Services.AddSingleton(Substitute.For<IFusionCache>());
        builder.Services.AddSingleton(Substitute.For<ISiteContext>());
        builder.Services.AddSingleton(Substitute.For<IMessageBus>());
        builder.Services.AddSingleton(Substitute.For<IPexelsService>());
        builder.Services.AddSingleton(Substitute.For<IGrainFactory>());
        builder.Services.AddScoped<BlockRenderCache>();
        builder.Services.AddScoped(_ => harness.Store);

        bootstrapper ??= Substitute.For<ISetupIdentityBootstrapper>();
        bootstrapper.BootstrapAsync(Arg.Any<SetupIdentityBootstrapRequest>(), Arg.Any<CancellationToken>())
            .Returns(new SetupIdentityBootstrapResult { CreatedAdmin = true });

        var module = new SetupModule();
        module.ConfigureServices(builder.Services, new ConfigurationBuilder().Build(), builder.Environment);
        var pagesModule = new PagesModule();
        pagesModule.ConfigureServices(builder.Services, new ConfigurationBuilder().Build(), builder.Environment);
        var blogModule = new PostsModule();
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
