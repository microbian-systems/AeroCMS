using Aero.Cms.Core;
using Aero.Cms.Modules.OutputCache.Caching;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.OutputCache;

/// <summary>
/// Registers the ASP.NET Core output caching middleware and defines named caching policies
/// for each content module (Pages, Blog, Docs).
///
/// Each policy is selected by placing [OutputCache(PolicyName = "...")] on the module's
/// Razor Page models — see Page.cshtml.cs, PostsIndexPage.cshtml.cs, PostsDetailPage.cshtml.cs,
/// DocsIndex.cshtml.cs, and Doc.cshtml.cs.
/// </summary>
[Module(nameof(OutputCacheModule))]
public sealed class OutputCacheModule : AeroWebModule, IAeroPipelineModule
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public override string Name => nameof(OutputCacheModule);
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
public override IReadOnlyList<string> Dependencies => [];
        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public override IReadOnlyList<string> Category => ["infrastructure", "performance"];
        /// <summary>
    /// Gets or sets the Tags.
    /// </summary>
public override IReadOnlyList<string> Tags => ["cache", "output-cache", "performance"];
        /// <summary>
    /// Gets or sets the Pipeline Order.
    /// </summary>
public int PipelineOrder => 200;

        /// <summary>
    /// ConfigureServices method.
    /// </summary>
public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        var cacheString = config?.GetConnectionString("cache");
        if (!string.IsNullOrWhiteSpace(cacheString))
        {
            // Output Cache intentionally uses its own Redis/Garnet store and key
            // namespace. It must not be routed through IDistributedCache because
            // output-cache tag eviction requires stronger atomic operations.
            services.AddStackExchangeRedisOutputCache(options =>
            {
                options.Configuration = cacheString;
                options.InstanceName = "aero:output:";
            });
        }

        services.AddOutputCache(options =>
        {
            // ── Pages module ──────────────────────────────────────────────
            // Used by: DynamicPageModel (Areas/Cms/Pages/Page.cshtml.cs)
            // Routes: / and /{slug}
            // Tags match PageCacheTags constants in Aero.Cms.Modules.Pages.Caching.
            options.AddPolicy("PagesPolicy", builder =>
                builder.AddPolicy<CmsOutputCachePolicy>()
                       .Expire(TimeSpan.FromMinutes(5))
                       .Tag("pages-list")
                       .SetVaryByQuery("slug"),
                excludeDefaultPolicy: true);

            // ── Blog module ───────────────────────────────────────────────
            // Used by: PostsIndexPageModel, PostsDetailPageModel
            // Routes: /blog, /blog/{slug}, /admin/blog/* (excluded by auth)
            // Tags match BlogCacheTags constants in Aero.Cms.Modules.Posts.Caching.
            options.AddPolicy("BlogPolicy", builder =>
                builder.AddPolicy<CmsOutputCachePolicy>()
                       .Expire(TimeSpan.FromMinutes(5))
                       .Tag("blog-index")
                       .SetVaryByQuery("p", "slug"),
                excludeDefaultPolicy: true);

            // Used by: PostsIndexPageModel.OnGetPostsPageAsync (HTMX partial)
            // Varies only by pagination param, not slug.
            options.AddPolicy("BlogPartialPolicy", builder =>
                builder.AddPolicy<CmsOutputCachePolicy>()
                       .Expire(TimeSpan.FromMinutes(5))
                       .Tag("blog-index")
                       .SetVaryByQuery("p"),
                excludeDefaultPolicy: true);

            // ── Docs module ───────────────────────────────────────────────
            // Used by: DocsIndexModel, DocModel
            // Routes: /docs, /docs/{*slug}
            // Longer TTL since documentation changes infrequently.
            options.AddPolicy("DocsPolicy", builder =>
                builder.AddPolicy<CmsOutputCachePolicy>()
                       .Expire(TimeSpan.FromMinutes(10))
                       .Tag("docs-index"),
                excludeDefaultPolicy: true);

            options.AddPolicy("DocsIndexPolicy", builder =>
                builder.AddPolicy<CmsOutputCachePolicy>()
                       .Expire(TimeSpan.FromMinutes(10))
                       .Tag("docs-index"),
                excludeDefaultPolicy: true);

            // ── Runtime content types ─────────────────────────────────────
            // Used by: PublicContentModel
            // Route: /content/{typeAlias}/{entrySlug}
            options.AddPolicy("ContentPublicPolicy", builder =>
                builder.AddPolicy<CmsOutputCachePolicy>()
                       .Expire(TimeSpan.FromMinutes(5))
                       .Tag("content-public"),
                excludeDefaultPolicy: true);
        });
    }

        /// <summary>
    /// ConfigurePipeline method.
    /// </summary>
public void ConfigurePipeline(IApplicationBuilder app)
        => app.UseOutputCache();
}
