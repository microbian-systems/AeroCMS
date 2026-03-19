using Aero.Cms.Core.Modules;
using Aero.Cms.Core.Pipelines;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Modules.Rewrite;

public class RewriteModule : AeroModuleBase
{
    public override string Name => nameof(RewriteModule);
    public override string Version => "0.0.5-alpha";
    public override string Author => "Microbians";
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["Infrastructure", "Routing"];
    public override IReadOnlyList<string> Tags => ["rewrite", "redirect", "routing", "url"];

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IPageSaveHook, SlugRewriteHook>();
    }

    public override void Run(IEndpointRouteBuilder endpoints)
    {
    }

    public override void Configure(IModuleBuilder builder)
    {
    }
}
