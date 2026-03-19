using Aero.Cms.Core.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Modules.Testing;

public class TestModule : AeroModuleBase
{
    public override string Name => "Test Module";
    public override string Version => "1.0.0";
    public override string Author => "Microbian Systems";
    public override IReadOnlyList<string> Dependencies => Array.Empty<string>();

    public override void Init(IEndpointRouteBuilder endpoints)
    {
        
    }

    public override void Configure(IModuleBuilder builder)
    {
        
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        
    }
}
