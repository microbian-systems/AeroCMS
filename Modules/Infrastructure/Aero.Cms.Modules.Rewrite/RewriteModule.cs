using Aero.Cms.Shared.Modules;
using Aero.Cms.Shared.Pipelines;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Modules.Rewrite;

public class RewriteModule : IModule
{
    public string Name => "Aero.Cms.Infrastructure.Rewrite";
    public string Version => "1.0.0";
    public string Author => "AeroCMS";
    public IReadOnlyList<string> Dependencies => [];

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IPageSaveHook, SlugRewriteHook>();
    }

    public void Configure(IModuleBuilder builder)
    {
    }
}
