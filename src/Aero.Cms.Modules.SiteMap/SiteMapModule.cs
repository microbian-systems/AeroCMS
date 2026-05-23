using Aero.Cms.Core;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;
using Marten;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.SiteMap;

[Module(nameof(SiteMapModule))]
public class SiteMapModule : AeroWebModule
{
    public override string Name => nameof(SiteMapModule);
    public override string Version => AeroConstants.Version;
    public override string Author => AeroConstants.Author;
    public override IReadOnlyList<string> Dependencies => [
        nameof(Aero.Cms.Modules.Posts.PostsModule),
        nameof(Aero.Cms.Modules.Pages.PagesModule),
        nameof(Aero.Cms.Modules.Docs.DocsModule),
        nameof(Aero.Cms.Modules.Cache.CacheModule)];
    public override IReadOnlyList<string> Category => ["SEO", "Content"];
    public override IReadOnlyList<string> Tags => ["sitemap", "seo", "google", "xml"];

    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        services.AddScoped<ISiteMapService, SiteMapService>();
    }

    /// <summary>
    /// Register Marten document lifecycle listener for sitemap cache invalidation.
    /// Listens for changes to page, post, and doc content documents.
    /// Replaces the previous Wolverine message handler approach which could not
    /// handle nested generic types (<c>AeroEvent&lt;T&gt;.PageCreated</c>, etc.).
    /// </summary>
    public override void Configure(IServiceProvider services, StoreOptions options)
    {
        options.Listeners.Add(new SitemapCacheListener(
            services.GetRequiredService<ZiggyCreatures.Caching.Fusion.IFusionCache>(),
            services.GetRequiredService<IHostEnvironment>(),
            services.GetRequiredService<ILogger<SitemapCacheListener>>()));
    }

    public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        builder.MapSitemapApi();
        return Task.CompletedTask;
    }
}
