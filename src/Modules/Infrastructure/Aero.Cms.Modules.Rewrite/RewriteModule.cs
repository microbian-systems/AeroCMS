using Aero.Cms.Core;
using Aero.Cms.Core.Modules;
using Aero.Cms.Web.Core.Modules;
using Aero.Cms.Web.Core.Pipelines;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.Rewrite;

public class RewriteModule : AeroModuleBase
{
    public override string Name => nameof(RewriteModule);
    public override string Version => AeroVersion.Version;
    public override string Author => AeroConstants.Author;
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["Infrastructure", "Routing"];
    public override IReadOnlyList<string> Tags => ["rewrite", "redirect", "routing", "url"];

    public override void ConfigureServices(IServiceCollection services, IConfiguration config = null, IHostEnvironment env = null)
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
