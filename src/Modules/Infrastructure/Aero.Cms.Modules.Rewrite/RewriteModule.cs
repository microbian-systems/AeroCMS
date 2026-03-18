using Aero.Cms.Core.Modules;
using Aero.Cms.Core.Pipelines;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Modules.Rewrite;

public class RewriteModule : ModuleBase
{
    public override string Name => "Rewrite";
    public override string Version => "1.0.0";
    public override string Author => "Microbian Systems";
    public override IReadOnlyList<string> Dependencies => [];

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IPageSaveHook, SlugRewriteHook>();
    }

    public override void Init(IEndpointRouteBuilder endpoints)
    {
    }

    public override void Configure(IModuleBuilder builder)
    {
    }
}
