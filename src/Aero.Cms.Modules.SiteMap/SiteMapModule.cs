using Aero.Cms.Core;
using Aero.Cms.Core.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.SiteMap;

public class SiteMapModule : AeroModuleBase
{
    public override string Name => nameof(SiteMapModule);
    public override string Version => AeroVersion.Version;
    public override string Author => AeroConstants.Author;
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["SEO", "Content"];
    public override IReadOnlyList<string> Tags => ["sitemap", "seo", "google", "xml"];

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
