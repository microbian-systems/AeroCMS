using Aero.Cms.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Modules.Health;

public class HealthModule : AeroModuleBase
{
    public override string Name{ get; }
    public override string Version { get; }
    public override string Author { get; }
    public override string Description => "";
    public override IReadOnlyList<string> Dependencies { get; }

    public override void ConfigureServices(IServiceCollection services, IConfiguration config = default)
    {
        services.AddHealthChecks();
    }

    public override void Init(IServiceProvider sp)
    {
        
    }

    public override Task InitAsync(IServiceProvider sp)
    {
        return Task.CompletedTask;
    }

    public override void Configure(IAeroModuleBuilder builder)
    {
        
    }

    public override void Run(IEndpointRouteBuilder app)
    {

    }

    public override Task RunAsync(IEndpointRouteBuilder app)
    {
        return Task.CompletedTask;
    }
}

