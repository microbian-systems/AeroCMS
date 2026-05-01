using Aero.Cms.Core;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.SiteMap;

public class SiteMapModule : AeroWebModule
{
    public override string Name => nameof(SiteMapModule);
    public override string Version => AeroConstants.Version;
    public override string Author => AeroConstants.Author;
    public override IReadOnlyList<string> Dependencies => [
        nameof(Aero.Cms.Modules.Blog.BlogModule),
        nameof(Aero.Cms.Modules.Pages.PagesModule),
        nameof(Aero.Cms.Modules.Docs.DocsModule),
        nameof(Aero.Cms.Modules.Cache.CacheModule)];
    public override IReadOnlyList<string> Category => ["SEO", "Content"];
    public override IReadOnlyList<string> Tags => ["sitemap", "seo", "google", "xml"];

    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        services.AddScoped<ISiteMapService, SiteMapService>();
    }

    public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        builder.MapSitemapApi();
        return Task.CompletedTask;
    }
}
