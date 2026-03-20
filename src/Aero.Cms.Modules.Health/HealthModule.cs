using Aero.Cms.Core;
using Aero.Cms.Core.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.Health;

public class HealthModule : AeroModuleBase
{
    public override string Name => nameof(HealthModule);
    public override string Version => AeroVersion.Version;
    public override string Author => AeroConstants.Author;
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["Infrastructure", "Monitoring"];
    public override IReadOnlyList<string> Tags => ["health", "monitoring", "diagnostics"];

    public override void ConfigureServices(IServiceCollection services, IConfiguration config = null, IHostEnvironment env = null)
    {
        services.AddHealthChecks();
    }

    public override async Task RunAsync(IEndpointRouteBuilder app)
    {
        await base.RunAsync(app);
        app.MapHealthChecks("/health");
    }
}
