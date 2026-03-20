using Aero.Cms.Core;
using Aero.Cms.Core.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.Jobs;

public class JobsModule : AeroModuleBase
{
    public override string Name => nameof(JobsModule);
    public override string Version => AeroVersion.Version;
    public override string Author => AeroConstants.Author;
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["Infrastructure", "BackgroundTasks"];
    public override IReadOnlyList<string> Tags => ["jobs", "background", "queue", "scheduler"];

    public override void ConfigureServices(IServiceCollection services, IConfiguration config = null, IHostEnvironment env = null)
    {

    }

    public override void Run(IEndpointRouteBuilder endpoints)
    {

    }

    public override void Configure(IModuleBuilder builder)
    {

    }
}
