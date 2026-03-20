using Aero.Cms.Core;
using Aero.Cms.Core.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Aero.Cms.Modules.Testing;

public class TestModule : AeroModuleBase
{
    public override string Name => nameof(TestModule);
    public override string Version => AeroVersion.Version;
    public override string Author => AeroConstants.Author;
    public override bool DisabledInProduction => true;
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["Development", "Testing"];
    public override IReadOnlyList<string> Tags => ["test", "debugging", "mock"];

    public override void Configure(IModuleBuilder builder)
    {
        log.Information("{a} called", nameof(Configure));
    }

    public override void ConfigureServices(IServiceCollection services, IConfiguration? config=null, IHostEnvironment? env=null)
    {
        log.Information("hello from testing module");
    }

    public override async Task RunAsync(IEndpointRouteBuilder endpoints)
    {
        log.Information("{a} called...", nameof(RunAsync));
        await Task.CompletedTask;
    }
}
