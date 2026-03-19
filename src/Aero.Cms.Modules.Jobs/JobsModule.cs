using Aero.Cms.Core.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Modules.Jobs;

public class JobsModule : AeroModuleBase
{
    public override string Name => nameof(JobsModule);
    public override string Version => "0.0.5-alpha";
    public override string Author => "Microbians";
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["Infrastructure", "BackgroundTasks"];
    public override IReadOnlyList<string> Tags => ["jobs", "background", "queue", "scheduler"];

    public override void ConfigureServices(IServiceCollection services)
    {

    }

    public override void Run(IEndpointRouteBuilder endpoints)
    {

    }

    public override void Configure(IModuleBuilder builder)
    {

    }
}
