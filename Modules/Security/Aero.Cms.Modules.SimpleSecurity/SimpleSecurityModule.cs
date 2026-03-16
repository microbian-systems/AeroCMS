using Aero.Cms.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Modules.SimpleSecurity;

public class SimpleSecurityModule : AeroModuleBase
{
    public override string Name=> "SimpleSecurity";
    public override string Version => "1.0.0";
    public override string Author => "Microbian Systems";
    public override IReadOnlyList<string> Dependencies => [];
    public override string Description => "";

    public void ConfigureServices(IServiceCollection services, IConfiguration config = default)
    {
        
    }

    public void Init(IServiceProvider sp)
    {
        
    }

    public void Run(IEndpointRouteBuilder app)
    {
        
    }

    public Task RunAsync(IEndpointRouteBuilder app)
    {
        return Task.CompletedTask;
    }

    public void ConfigureServices(IServiceCollection services)
    {
    }

    public void Configure(IAeroModuleBuilder builder)
    {
    }
}
