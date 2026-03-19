using Aero.Cms.Core.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Modules.Security;

public class SecurityModule : AeroModuleBase
{
    public override string Name => "Security";
    public override string Version => "0.0.5-alpha";
    public override string Author => "Microbians";
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => [];
    public override IReadOnlyList<string> Tags => [];

    public override void ConfigureServices(IServiceCollection services)
    {
    }

    public override void Run(IEndpointRouteBuilder endpoints)
    {
    }

    public override void Configure(IModuleBuilder builder)
    {
        // Admin UI registration
    }
}
