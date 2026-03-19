using Aero.Cms.Core.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Modules.Testing;

public class TestModule : AeroModuleBase
{
    public override string Name => nameof(TestModule);
    public override string Version => "0.0.5-alpha";
    public override string Author => "Microbians";
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["Development", "Testing"];
    public override IReadOnlyList<string> Tags => ["test", "debugging", "mock"];

    public override void Run(IEndpointRouteBuilder endpoints)
    {

    }

    public override void Configure(IModuleBuilder builder)
    {

    }

    public override void ConfigureServices(IServiceCollection services)
    {

    }
}
