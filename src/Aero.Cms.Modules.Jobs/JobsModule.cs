using Aero.Cms.Core.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Modules.Jobs;

public class JobsModule : ModuleBase
{
    public override string Name => "Aero Jobs";
    public override string Version => "1.0.0";
    public override string Author => "Aero.Cms";
    public override IReadOnlyList<string> Dependencies => Array.Empty<string>();

    public override void ConfigureServices(IServiceCollection services)
    {
        
    }

    public override void Init(IEndpointRouteBuilder endpoints)
    {
        
    }

    public override void Configure(IModuleBuilder builder)
    {
        
    }
}
