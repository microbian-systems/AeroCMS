using Aero.Cms.Core.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Modules.Health;

public class HealthModule : AeroModuleBase
{
    public override string Name => nameof(HealthModule);
    public override string Version => "0.0.5-alpha";
    public override string Author => "Microbians";
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["Infrastructure", "Monitoring"];
    public override IReadOnlyList<string> Tags => ["health", "monitoring", "diagnostics"];

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddHealthChecks();
    }

    public override void Run(IEndpointRouteBuilder endpoints)
    {

    }

    public override void Configure(IModuleBuilder builder)
    {

    }
}
