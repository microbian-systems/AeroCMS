using Aero.Cms.Core;
using Aero.Cms.Core.Modules;
using Aero.Cms.Modules.Admin.Api;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.Admin;

/// <summary>
/// Aero CMS Admin module - provides admin functionality for publishing and previewing content.
/// </summary>
public sealed class AdminModule : AeroModuleBase
{
    public override string Name => nameof(AdminModule);

    public override string Version => AeroVersion.Version;

    public override string Author => AeroConstants.Author;

    public override IReadOnlyList<string> Dependencies => [nameof(BlogModule), nameof(PagesModule)];

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
        builder.MapCategoriesApi();
        builder.MapTagsApi();
        builder.MapFilesApi();
        builder.MapUsersApi();
        builder.MapThemesApi();
        builder.MapSettingsApi();
        builder.MapProfileApi();

        return Task.CompletedTask;
    }
}
