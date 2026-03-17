using Aero.Cms.Core;
using Aero.Cms.Core.Pipelines;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Modules.Rewrite;

public class RewriteModule : AeroModuleBase
{
    public override string Name=> "Rewrite";
    public override string Version => "1.0.0";
    public override string Author => "Microbian Systems";
    public override IReadOnlyList<string> Dependencies => [];
    public override string Description => "";

    public override void ConfigureServices(IServiceCollection services, IConfiguration config = default)
    {
        services.AddScoped<IPageSaveHook, SlugRewriteHook>();
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

    public override void Configure(IAeroModuleBuilder builder)
    {
    }
}
