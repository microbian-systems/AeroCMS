using Aero.Cms.Core;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;
using AeroDB.Sable;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.SiteMap;

/// <summary>
/// Represents a class for SiteMapModule.
/// </summary>
[Module(nameof(SiteMapModule))]
public class SiteMapModule : AeroWebModule, IConfigureAeroDB
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public override string Name => nameof(SiteMapModule);
        /// <summary>
    /// Gets or sets the Version.
    /// </summary>
public override string Version => AeroConstants.Version;
        /// <summary>
    /// Gets or sets the Author.
    /// </summary>
public override string Author => AeroConstants.Author;
        /// <summary>
    /// Gets or sets the Dependencies.
    /// </summary>
public override IReadOnlyList<string> Dependencies => [
        nameof(Aero.Cms.Modules.Posts.PostsModule),
        nameof(Aero.Cms.Modules.Pages.PagesModule),
        nameof(Aero.Cms.Modules.Docs.DocsModule),
        nameof(Aero.Cms.Modules.Cache.CacheModule)];
        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public override IReadOnlyList<string> Category => ["SEO", "Content"];
        /// <summary>
    /// Gets or sets the Tags.
    /// </summary>
public override IReadOnlyList<string> Tags => ["sitemap", "seo", "google", "xml"];

        /// <summary>
    /// ConfigureServices method.
    /// </summary>
public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        services.AddScoped<ISiteMapService, SiteMapService>();
    }

    /// <summary>
    /// Register AeroDB document lifecycle listener for sitemap cache invalidation.
    /// Listens for changes to page, post, and doc content documents.
    /// Replaces the previous Wolverine message handler approach which could not
    /// handle nested generic types (<c>AeroEvent&lt;T&gt;.PageCreated</c>, etc.).
    /// </summary>
    public void Configure(StoreOptions options)
    {
    }

        /// <summary>
    /// Configure method.
    /// </summary>
public void Configure(IServiceProvider services, StoreOptions options)
    {
        options.Listeners.Add(new SitemapCacheListener(
            services.GetRequiredService<ZiggyCreatures.Caching.Fusion.IFusionCache>(),
            services.GetRequiredService<IHostEnvironment>(),
            services.GetRequiredService<ILogger<SitemapCacheListener>>()));
    }

        /// <summary>
    /// RunAsync method.
    /// </summary>
public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        builder.MapSitemapApi();
        return Task.CompletedTask;
    }
}
