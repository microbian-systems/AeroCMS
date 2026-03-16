using Aero.Cms.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Modules.Security;

public class SecurityModule : AeroModuleBase
{
    public override string Name=> "Security";
    public override string Version => "1.0.0";
    public override string Author => "Microbian Systems";
    public override IReadOnlyList<string> Dependencies => [];
    public override string Description => "";

    public override void ConfigureServices(IServiceCollection services, IConfiguration config = default)
    {
        
    }

    public override void Init(IServiceProvider sp)
    {
        
    }

    public override void Run(IEndpointRouteBuilder app)
    {
        
    }

    public override Task RunAsync(IEndpointRouteBuilder app)
    {
        return Task.CompletedTask;
    }


    public override void Configure(IAeroModuleBuilder builder)
    {
        // Admin UI registration
    }
}
