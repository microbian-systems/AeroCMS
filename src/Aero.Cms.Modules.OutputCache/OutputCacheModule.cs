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
    public override string Name => nameof(OutputCacheModule);
    public override string Version => AeroConstants.Version;
    public override string Author => AeroConstants.Author;
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["infrastructure", "performance"];
    public override IReadOnlyList<string> Tags => ["cache", "output-cache", "performance"];
    public int PipelineOrder => 200;

    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        // TODO: Switch to Redis output cache store with AddStackExchangeRedisOutputCache()
        // when a shared/distributed output cache is needed across server nodes.
        // This replaces the in-process MemoryCache default with a Redis-backed store
        // so cached responses survive restarts and are shared across multiple web instances.
        //
        // NuGet: Microsoft.AspNetCore.OutputCaching.StackExchangeRedis
        //
        // builder.Services.AddStackExchangeRedisOutputCache(options =>
        // {
        //     options.Configuration = config!.GetConnectionString("cache");
        //     options.InstanceName = "AeroCmsOutput";
        // });

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
        });
    }

    public void ConfigurePipeline(IApplicationBuilder app)
        => app.UseOutputCache();
}
