using Aero.Cms.Core;
using Aero.Cms.Modules.Headless.Api.v1;
using Aero.Cms.Web.Core.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.Headless;

/// <summary>
/// Aero CMS Admin module - provides admin functionality for publishing and previewing content.
/// </summary>
public sealed class HeadlessModule : AeroModuleBase
{
    public override string Name => nameof(HeadlessModule);

    public override string Version => AeroVersion.Version;

    public override string Author => AeroConstants.Author;

    public override IReadOnlyList<string> Dependencies => [];

    public override IReadOnlyList<string> Category => ["admin", "management"];

    public override IReadOnlyList<string> Tags => ["admin", "management", "cms", "publish", "preview"];

    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        // Admin module services will be registered here as APIs are added
    }

    public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        builder.MapPublishApi();
        builder.MapPreviewApi();
        builder.MapBlogApi();
        builder.MapPagesApi();
        builder.MapMediaApi();
        builder.MapDashboardApi();
        builder.MapNavigationsApi();
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

        return Task.CompletedTask;
    }
}
