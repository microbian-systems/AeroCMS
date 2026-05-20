using Aero.Cms.Abstractions.Audit;
using Aero.Cms.Core;
using Aero.Cms.Modules.Headless.Areas.Api.v1;
using Aero.Cms.Web.Core.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Scalar.AspNetCore;
using Aero.Modular;

namespace Aero.Cms.Modules.Headless;

/// <summary>
/// Aero CMS Admin module - provides admin functionality for publishing and previewing content.
/// </summary>
[Module(nameof(HeadlessModule))]
public sealed class HeadlessModule : AeroWebModule
{
    public override string Name => nameof(HeadlessModule);

    public override string Version => AeroConstants.Version;

    public override string Author => AeroConstants.Author;

    public override IReadOnlyList<string> Dependencies => [];

    public override IReadOnlyList<string> Category => ["admin", "management"];

    public override IReadOnlyList<string> Tags => ["admin", "management", "cms", "publish", "preview"];

    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        // todo - check settings if openapi should be publicly available
        //if(env.IsDevelopment())
        services.AddOpenApi();

        services.AddScoped<IAuditService, AuditService>();
    }


    // todo - move the APIs to their respective modules. No need for the headless module to own all the APIs, and it will be easier to maintain if the APIs are owned by the modules that own the aggregates and projections they interact with. The only reason they are all here is because of the way the
    // module system was originally designed, but now that we have a better understanding of how to use it, we can refactor to a more modular approach.
    public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        var scope = builder.ServiceProvider.CreateAsyncScope();
        var env = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();

        builder.MapPreviewApi();
        builder.MapBlogApi();
        builder.MapPagesApi();
        builder.MapPagesTreeApi();
        builder.MapAuditApi();
        builder.MapMediaApi();
        builder.MapDashboardApi();
        // Navigation is now owned by Aero.Cms.Modules.Navigation so it can map
        // the event-sourced manager API from the same module that owns the
        // aggregate, projections, and Marten configuration.
        builder.MapModulesApi();
        builder.MapDocsApi();
        builder.MapCategoriesApi();
        builder.MapTagsApi();
        builder.MapFilesApi();
        builder.MapUsersApi();
        builder.MapThemesApi();
        builder.MapSettingsApi();
        builder.MapProfileApi();
        builder.MapBlocksApi();
        builder.MapContentTypesApi();
        builder.MapContentItemsApi();
        builder.MapJwtApi();
        builder.MapAuthApi();
        builder.MapAliasesApi();



        return Task.CompletedTask;
    }
}
