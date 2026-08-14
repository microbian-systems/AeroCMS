using Aero.Cms.Hosting.Defaults;
using Aero.Cms.Web.Bootstrap;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Hosting.Tests;

public sealed class AeroCmsPipelineStageTests
{
    [Test]
    public async Task Reusable_CMS_layout_is_discoverable_without_the_standalone_web_host()
    {
        await using var app = await CreateAppAsync();
        var viewEngine = app.Services.GetRequiredService<IRazorViewEngine>();

        var result = viewEngine.GetView(
            executingFilePath: null,
            viewPath: "/Views/Shared/_CmsLayout.cshtml",
            isMainPage: true);

        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Site_stage_requires_the_Aero_CMS_routing_boundary()
    {
        await using var app = await CreateAppAsync();

        var exception = await Assert.That(app.UseAeroCmsSiteAndLocalization)
            .Throws<InvalidOperationException>();

        await Assert.That(exception!.Message).Contains("AEROCMS_PIPELINE_ROUTING_REQUIRED");
    }

    [Test]
    public async Task Routing_stage_rejects_duplicate_registration()
    {
        await using var app = await CreateAppAsync();
        app.UseAeroCmsRouting();

        var exception = await Assert.That(app.UseAeroCmsRouting)
            .Throws<InvalidOperationException>();

        await Assert.That(exception!.Message).Contains("AEROCMS_PIPELINE_STAGE_DUPLICATE");
    }

    [Test]
    public async Task Terminal_stage_requires_mapped_CMS_endpoints()
    {
        await using var app = await CreateAppAsync();

        var exception = await Assert.That(app.UseAeroCmsTerminalPipeline)
            .Throws<InvalidOperationException>();

        await Assert.That(exception!.Message).Contains("AEROCMS_PIPELINE_STAGE_ORDER");
    }

    [Test]
    public async Task Routing_stage_exposes_Hydro_scripts_to_host_static_file_middleware()
    {
        await using var app = CreateRoutingTestApp();
        app.UseAeroCmsRouting();
        app.UseStaticFiles();
        app.Run(static context =>
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        });

        await app.StartAsync();
        var server = app.Services.GetRequiredService<IServer>();
        var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient { BaseAddress = new Uri(address) };
        using var response = await client.GetAsync("/hydro/hydro.js");
        var script = await response.Content.ReadAsStringAsync();
        using var alpineResponse = await client.GetAsync("/hydro/alpine.js");

        await Assert.That(response.StatusCode).IsEqualTo(System.Net.HttpStatusCode.OK);
        await Assert.That(script).Contains("function HydroCore()");
        await Assert.That(alpineResponse.StatusCode).IsEqualTo(System.Net.HttpStatusCode.OK);
    }

    [Test]
    public async Task Routing_stage_does_not_expose_Hydro_scripts_when_Hydro_is_disabled()
    {
        await using var app = CreateRoutingTestApp(enableHydro: false);
        app.UseAeroCmsRouting();
        app.UseStaticFiles();
        app.Run(static context =>
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        });

        await app.StartAsync();
        var server = app.Services.GetRequiredService<IServer>();
        var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient { BaseAddress = new Uri(address) };
        using var response = await client.GetAsync("/hydro/hydro.js");

        await Assert.That(response.StatusCode).IsEqualTo(System.Net.HttpStatusCode.NotFound);
    }

    private static WebApplication CreateRoutingTestApp(bool enableHydro = true)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(new AeroCmsOptions { EnableHydro = enableHydro });
        builder.Services.AddSingleton<AeroCmsPipelineState>();

        return builder.Build();
    }

    private static async Task<WebApplication> CreateAppAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["AeroCms:Setup:RunHandoff"] = "false";
        builder.Configuration["AeroCms:Bootstrap:State"] = "Running";
        builder.Configuration["AeroCms:Bootstrap:SetupComplete"] = "true";
        builder.Configuration["AeroCms:Bootstrap:SeedComplete"] = "true";
        builder.Configuration["AeroCms:Bootstrap:HasBootstrapConfig"] = "true";
        builder.Configuration["AeroCms:Bootstrap:RequestedManagerAuthenticationProvider"] = "local";
        builder.Configuration["AeroCms:Bootstrap:RequestedMemberAuthenticationProvider"] = "disabled";
        builder.Configuration["AeroCms:Infrastructure:DatabaseMode"] = "Embedded";
        builder.Configuration["AeroCms:Infrastructure:CacheMode"] = "Local";
        builder.Configuration["AeroCms:Infrastructure:SecretProvider"] = "Local Certificate";
        await builder
            .AddAeroCms(AeroCmsDefaultCatalog.Catalog)
            .WithSetupSettingsDirectory(builder.Environment.ContentRootPath)
            .RegisterHostAsync<AeroCmsPipelineStageTests>();
        return builder.Build();
    }
}
