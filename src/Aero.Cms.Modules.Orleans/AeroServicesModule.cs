using Aero.Cms.Core;
using Aero.Cms.Web.Core.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.Orleans;

public class AeroServicesModule : AeroModuleBase
{
    public override string Name => nameof(AeroServicesModule);
    public override string Version => AeroVersion.Version;
    public override string Author => AeroConstants.Author;
    public override string? Description => "Uses virtual grains (actors) for service communication";
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["aero"];
    public override IReadOnlyList<string> Tags => ["aero", "services"];

    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        base.ConfigureServices(services, config, env);
    }

    public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        return base.RunAsync(builder);
    }
}