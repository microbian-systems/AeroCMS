using Aero.Cms.Core.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Modules.Security;

public class SecurityModule : ModuleBase
{
    public override string Name => "Security";
    public override string Version => "1.0.0";
    public override string Author => "Microbian Systems";
    public override IReadOnlyList<string> Dependencies => [];

    public override void ConfigureServices(IServiceCollection services)
    {
    }

    public override void Init(IEndpointRouteBuilder endpoints)
    {
    }

    public override void Configure(IModuleBuilder builder)
    {
        // Admin UI registration
    }
}
