using Aero.Cms.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Modules.Jobs;

public class JobsModule : AeroModuleBase
{
    public override string Name => "";

    public override string Version => "";

    public override string Author => "";
    public override string Description => "";

    public override IReadOnlyList<string> Dependencies => [];

    public override void Configure(IAeroModuleBuilder builder)
    {
        
    }

    public override void ConfigureServices(IServiceCollection services, IConfiguration config = null)
    {
        
    }

    public override void Init(IServiceProvider sp)
    {
        
    }

    public override Task InitAsync(IServiceProvider sp)
    {
        return Task.CompletedTask;

    }

    public override void Run(IEndpointRouteBuilder app)
    {
        
    }

    public override Task RunAsync(IEndpointRouteBuilder app)
    {
        return Task.CompletedTask;
    }
}
