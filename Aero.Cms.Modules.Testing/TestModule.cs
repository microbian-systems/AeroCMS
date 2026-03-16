using Aero.Cms.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Modules.Testing;

public class TestModule : AeroModuleBase
{
    public override string Name => "Test Module";
    public override string Version => "1.0.0";
    public override string Author => "Microbian Systems";
    public override IReadOnlyList<string> Dependencies { get; } = [];
    public override string Description => "This module is for testing purposes only";

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

    public override Task RunAsync(IEndpointRouteBuilder app)
    {
        return Task.CompletedTask;
    }

    public override void Run(IEndpointRouteBuilder app)
    {
        
    }

    public override void ConfigureServices(IServiceCollection services, IConfiguration config = default)
    {
        
    }
}
