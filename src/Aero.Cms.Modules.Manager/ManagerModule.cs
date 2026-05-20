using Aero.Cms.Core;
using Aero.Cms.Modules.Manager.Areas.Api.v1;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.Manager;

[Module(nameof(ManagerModule))]
public class ManagerModule : AeroWebModule
{
    public override string Name { get; } = nameof(ManagerModule);
    public override string Version { get; } = AeroConstants.Version;
    public override string Author { get; } = AeroConstants.Author;
    public override IReadOnlyList<string> Dependencies { get; } = [];
    public override IReadOnlyList<string> Category { get; } = [];
    public override IReadOnlyList<string> Tags { get; } = [];

    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        base.ConfigureServices(services, config, env);
    }

    public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        builder.MapDashboardApi();
        builder.MapPreviewBlockFragmentApi();
        return Task.CompletedTask;
    }
}