using Aero.Cms.Core.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Modules.SimpleSecurity;

public class SimpleSecurityModule : AeroModuleBase
{
    public override string Name => "SimpleSecurity";
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
    }
}
