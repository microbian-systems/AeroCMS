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
/// Registers sitemap generation, cache invalidation, and public SEO endpoints.
/// </summary>
[Module(nameof(SiteMapModule))]
public class SiteMapModule : AeroWebModule, IConfigureAeroDB
{
        /// <inheritdoc />
public override string Name => nameof(SiteMapModule);
        /// <inheritdoc />
public override string Version => AeroConstants.Version;
        /// <inheritdoc />
public override string Author => AeroConstants.Author;
        /// <inheritdoc />
public override IReadOnlyList<string> Dependencies => [
        nameof(Aero.Cms.Modules.Posts.PostsModule),
        nameof(Aero.Cms.Modules.Pages.PagesModule),
        nameof(Aero.Cms.Modules.Docs.DocsModule),
        nameof(Aero.Cms.Modules.Cache.CacheModule)];
        /// <inheritdoc />
public override IReadOnlyList<string> Category => ["SEO", "Content"];
        /// <inheritdoc />
public override IReadOnlyList<string> Tags => ["sitemap", "seo", "google", "xml"];

        /// <summary>
    /// Registers a scoped sitemap service.
    /// </summary>
    /// <param name="services">The collection that receives the service registration.</param>
    /// <param name="config">Module configuration; not used.</param>
    /// <param name="env">The host environment; not used.</param>
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
    /// <param name="options">Store options; currently left unchanged by this overload.</param>
    public void Configure(StoreOptions options)
    {
    }

        /// <summary>
    /// Adds a production-aware sitemap cache listener to the document store.
    /// </summary>
    /// <param name="services">The provider used to resolve the listener dependencies.</param>
    /// <param name="options">The store options whose listener collection is mutated.</param>
public void Configure(IServiceProvider services, StoreOptions options)
    {
        options.Listeners.Add(new SitemapCacheListener(
            services.GetRequiredService<ZiggyCreatures.Caching.Fusion.IFusionCache>(),
            services.GetRequiredService<IHostEnvironment>(),
            services.GetRequiredService<ILogger<SitemapCacheListener>>()));
    }

        /// <summary>
    /// Adds the public sitemap and robots endpoints to the host.
    /// </summary>
    /// <param name="builder">The endpoint route builder to mutate.</param>
    /// <returns>A task already completed after synchronous route registration.</returns>
public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        builder.MapSitemapApi();
        return Task.CompletedTask;
    }
}
