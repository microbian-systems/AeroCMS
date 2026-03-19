using Aero.Cms.Core.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Modules.SiteMap;

public class SiteMapModule : AeroModuleBase
{
    public override string Name => nameof(SiteMapModule);
    public override string Version => "0.0.5-alpha";
    public override string Author => "Microbians";
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["SEO", "Content"];
    public override IReadOnlyList<string> Tags => ["sitemap", "seo", "google", "xml"];

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
