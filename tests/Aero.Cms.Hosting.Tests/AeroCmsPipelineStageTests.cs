using Aero.Cms.Hosting.Defaults;
using Aero.Cms.Web.Bootstrap;
using Microsoft.AspNetCore.Builder;

namespace Aero.Cms.Hosting.Tests;

public sealed class AeroCmsPipelineStageTests
{
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
