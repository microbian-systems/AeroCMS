using Aero.Cms.Modules.Setup;
using Aero.Cms.Modules.Setup.Bootstrap;
using Aero.Cms.Modules.Setup.Endpoints;
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
using Aero.Cms.Core.Entities;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Core.Http;
using Aero.Services.Images;
using NSubstitute;
using Shouldly;
using Orleans;
using Wolverine;
using ZiggyCreatures.Caching.Fusion;
using Radzen;
using System.Text.Json;

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
    public async Task Setup_host_expires_the_previous_selected_site_cookie()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<SetupStateDocument>().WithSchema<PageDocument>()
            .WithSchema<PostDocument>().WithSchema<ContentSlugDocument>();
        await harness.InitializeAsync();
        await using var app = await CreateAppAsync(harness);
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/setup");
        request.Headers.Add("Cookie", "AeroCms.SiteId=999999999999999999");

        using var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        var setCookie = string.Join(
            Environment.NewLine,
            response.Headers.GetValues("Set-Cookie"));
        setCookie.ShouldContain("AeroCms.SiteId=");
        setCookie.ShouldContain("expires=");
    }

    [Test]
    public async Task Running_setup_status_hands_off_the_created_site_to_cookie_and_browser()
    {
        const long siteId = 1_530_221_140_281_556_994;
        var initialization = Substitute.For<ISetupInitializationService>();
        initialization.GetBootstrapState().Returns(new BootstrapState
        {
            State = BootstrapStates.Running,
            SetupComplete = true,
            SeedComplete = true,
            DatabaseMode = "Server",
            CacheMode = "Server",
            HasBootstrapConfig = true
        });
        var setupStateStore = Substitute.For<ISetupStateStore>();
        setupStateStore.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SetupStateDocument?>(new SetupStateDocument
            {
                IsComplete = true,
                CreatedSiteId = siteId,
                SiteName = "Contoso"
            }));

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(initialization);
        builder.Services.AddSingleton(setupStateStore);

        await using var app = builder.Build();
        app.MapSetupStatusEndpoints();
        await app.StartAsync();
        using var client = app.GetTestClient();

        using var response = await client.GetAsync("/setup/status");
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        payload.RootElement.GetProperty("createdSiteId").GetString()
            .ShouldBe(siteId.ToString());
        payload.RootElement.GetProperty("siteName").GetString()
            .ShouldBe("Contoso");

        var setCookie = string.Join(
            Environment.NewLine,
            response.Headers.GetValues("Set-Cookie"));
        setCookie.ShouldContain($"AeroCms.SiteId={siteId}");
        setCookie.ShouldContain("httponly");
        setCookie.ShouldContain("path=/");
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
        html.ShouldContain("System Setup");
        html.ShouldContain("Welcome to");
        html.ShouldContain("CMS Main Info");
        html.ShouldContain("name=\"Input.SiteName\"");
        html.ShouldContain("name=\"Input.HomepageTitle\"");
        html.ShouldContain("name=\"Input.BlogName\"");
        html.ShouldContain("_framework/blazor.web.js");
    }

    [Test]
    public async Task Setup_component_route_precedes_the_public_page_catch_all()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<SetupStateDocument>().WithSchema<PageDocument>()
            .WithSchema<PostDocument>().WithSchema<ContentSlugDocument>();
        await harness.InitializeAsync();
        await using var app = await CreateAppAsync(harness);
        using var client = app.GetTestClient();

        using var response = await client.GetAsync("/setup");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        html.ShouldContain("System Setup");
        html.ShouldContain("_framework/blazor.web.js");
        html.ShouldNotContain("data-aero-page-styles");
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
        builder.Configuration["AeroCms:Configuration:SettingsDirectory"] = Path.Combine(
            Path.GetTempPath(),
            "aero-cms-setup-tests",
            Guid.NewGuid().ToString("N"));

        builder.WebHost.UseTestServer();
        builder.Services.AddLogging();
        builder.Services.AddControllersWithViews();
        builder.Services.AddAuthentication();
        builder.Services.AddAuthorization();
        builder.Services.AddHealthChecks();
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();
        builder.Services.AddRadzenComponents();
        builder.Services.AddRazorPages()
            .AddApplicationPart(typeof(SetupModule).Assembly);

        harness ??= new SableTestHarness()
            .WithSchema<SetupStateDocument>().WithSchema<PageDocument>()
            .WithSchema<PostDocument>().WithSchema<ContentSlugDocument>();
        await harness.InitializeAsync();

        builder.Services.AddScoped(_ => harness.Session);
        builder.Services.AddScoped<IDocumentSession>(_ => harness.Session);
        builder.Services.AddScoped<IQuerySession>(_ => harness.Session);
        builder.Services.AddSingleton(Substitute.For<IFusionCache>());
        builder.Services.AddSingleton(Substitute.For<ISiteContext>());
        builder.Services.AddSingleton(Substitute.For<ISiteStyleProfileResolver>());
        builder.Services.AddSingleton(Substitute.For<IMessageBus>());
        builder.Services.AddSingleton(Substitute.For<IPexelsService>());
        builder.Services.AddSingleton(Substitute.For<IGrainFactory>());
        builder.Services.AddScoped(_ => harness.Store);

        bootstrapper ??= Substitute.For<ISetupIdentityBootstrapper>();
        bootstrapper.BootstrapAsync(Arg.Any<SetupIdentityBootstrapRequest>(), Arg.Any<CancellationToken>())
            .Returns(new SetupIdentityBootstrapResult { CreatedAdmin = true });

        var module = new SetupModule();
        module.ConfigureServices(builder.Services, builder.Configuration, builder.Environment);
        var pagesModule = new PagesModule();
        pagesModule.ConfigureServices(builder.Services, builder.Configuration, builder.Environment);
        var blogModule = new PostsModule();
        blogModule.ConfigureServices(builder.Services, builder.Configuration, builder.Environment);
        builder.Services.RemoveAll<ISetupIdentityBootstrapper>();
        builder.Services.AddSingleton(bootstrapper);
        var healthModule = new Aero.Cms.Modules.Health.HealthModule();
        healthModule.ConfigureServices(builder.Services, builder.Configuration, builder.Environment);

        var app = builder.Build();

        app.MapDefaultEndpoints();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseSetupSiteSelectionReset();
        app.UseCmsSetupGate();
        if (enableAntiforgery)
        {
            app.UseAntiforgery();
        }

        app.MapRazorPages();
        app.MapRazorComponents<Aero.Cms.Modules.Setup.Areas.Setup.Pages.SetupRoot>()
            .AddInteractiveServerRenderMode();
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
