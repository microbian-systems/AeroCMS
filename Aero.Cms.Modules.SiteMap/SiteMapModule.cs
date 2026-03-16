using Aero.Cms.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Modules.SiteMap;

public class SiteMapModule : AeroModuleBase
{
    public override string Name{ get; } = nameof(SiteMapModule);
    public override string Version { get; } = "1.0.0";
    public override string Author { get; } = "Microbian Systems";
    public override string Description => "";
    public override IReadOnlyList<string> Dependencies { get; } = [];

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
        
    }
}
