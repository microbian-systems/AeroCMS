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
/// Registers the Redis-compatible ASP.NET Core output-cache store, CMS response policies,
/// and output-cache middleware.
/// </summary>
/// <remarks>
/// <para>
/// This module owns rendered HTTP-response caching. It is distinct from
/// <c>Aero.Cms.Modules.Cache</c>, which configures FusionCache for application data and
/// separately coordinates selected tag evictions.
/// </para>
/// <para>
/// Responses are stored under the <c>aero:output:</c> Redis key namespace. The named policies
/// use <see cref="CmsOutputCachePolicy"/> and resource locking. <c>PagesPolicy</c>,
/// <c>BlogPolicy</c>, <c>BlogPartialPolicy</c>, and <c>ContentPublicPolicy</c> expire after five
/// minutes; <c>DocsPolicy</c> and <c>DocsIndexPolicy</c> expire after ten minutes. Their coarse
/// tags are respectively <c>pages-list</c>, <c>blog-index</c>, <c>blog-index</c>,
/// <c>content-public</c>, <c>docs-index</c>, and <c>docs-index</c>.
/// </para>
/// <para>
/// <c>PagesPolicy</c> additionally configures query variation for <c>slug</c>;
/// <c>BlogPolicy</c> for <c>p</c> and <c>slug</c>; and <c>BlogPartialPolicy</c> for <c>p</c>.
/// The remaining policies retain the custom policy's all-query variation. Every policy also
/// varies by the origin, request path, and current UI culture established by
/// <see cref="CmsOutputCachePolicy"/>.
/// </para>
/// <para>
/// Policies take effect only on endpoints that select them, for example with
/// <c>OutputCacheAttribute.PolicyName</c>. The coarse tags are not site-scoped, so evicting one
/// removes every matching entry in this output-cache namespace. The registrations do not
/// provide transactional invalidation, and the module does not claim coherence with
/// FusionCache or any other cache layer.
/// </para>
/// </remarks>
[Module(nameof(OutputCacheModule))]
public sealed class OutputCacheModule : AeroWebModule, IAeroPipelineModule
{
    /// <summary>
    /// Gets the fixed module-discovery name.
    /// </summary>
    public override string Name => nameof(OutputCacheModule);

    /// <summary>
    /// Gets the Aero CMS version reported by this module.
    /// </summary>
    public override string Version => AeroConstants.Version;

    /// <summary>
    /// Gets the Aero CMS author metadata reported by this module.
    /// </summary>
    public override string Author => AeroConstants.Author;

    /// <summary>
    /// Gets the module names that must load before this module.
    /// </summary>
    /// <remarks>The output-cache module declares no module dependency.</remarks>
    public override IReadOnlyList<string> Dependencies => [];

    /// <summary>
    /// Gets the module-discovery categories.
    /// </summary>
    public override IReadOnlyList<string> Category => ["infrastructure", "performance"];

    /// <summary>
    /// Gets the module-discovery tags.
    /// </summary>
    public override IReadOnlyList<string> Tags => ["cache", "output-cache", "performance"];

    /// <summary>
    /// Gets the ordering value used when the Aero CMS host composes module middleware.
    /// </summary>
    /// <remarks>
    /// The value is 200. The host orders pipeline modules first by this value and then by their
    /// module order; the host still controls where the complete module pipeline is inserted.
    /// </remarks>
    public int PipelineOrder => 200;

    /// <summary>
    /// Registers the Redis-compatible output-cache store and the named CMS output-cache policies.
    /// </summary>
    /// <param name="services">The service collection to receive output-cache registrations.</param>
    /// <param name="config">
    /// Configuration containing <c>AeroCms:Bootstrap:CacheMode</c> and the <c>cache</c>
    /// connection string. A missing configuration uses local mode and
    /// <c>localhost:33333</c>.
    /// </param>
    /// <param name="env">
    /// The host environment. This implementation does not inspect the environment.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// <c>AeroCms:Bootstrap:CacheMode</c> is neither <c>Local</c> nor <c>Server</c>, or server
    /// mode has no non-blank <c>cache</c> connection string.
    /// </exception>
    /// <remarks>
    /// Local mode falls back to <c>localhost:33333</c> when the connection string is blank.
    /// Both supported modes register <c>AddStackExchangeRedisOutputCache</c> with the resolved
    /// endpoint. Connection establishment and runtime store failures remain the responsibility
    /// of the registered provider; this method does not contact the cache server.
    /// </remarks>
    public override void ConfigureServices(
        IServiceCollection services,
        IConfiguration? config = null,
        IHostEnvironment? env = null)
    {
        var cacheMode = config?.GetValue<string>("AeroCms:Bootstrap:CacheMode") ?? "Local";
        var cacheString = config?.GetConnectionString("cache");
        if (string.IsNullOrWhiteSpace(cacheString)
            && cacheMode.Equals("Local", StringComparison.OrdinalIgnoreCase))
        {
            cacheString = "localhost:33333";
        }

        if (!cacheMode.Equals("Local", StringComparison.OrdinalIgnoreCase)
            && !cacheMode.Equals("Server", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Unsupported cache mode '{cacheMode}'. Expected 'Local' or 'Server'.");
        }

        if (string.IsNullOrWhiteSpace(cacheString))
        {
            throw new InvalidOperationException(
                $"Cache mode '{cacheMode}' requires a Redis-compatible connection string.");
        }

        // Output Cache intentionally uses its own Redis/Garnet store and key
        // namespace. It must not be routed through IDistributedCache because
        // output-cache tag eviction requires stronger atomic operations.
        services.AddStackExchangeRedisOutputCache(options =>
        {
            options.Configuration = cacheString;
            options.InstanceName = "aero:output:";
        });

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
    /// Adds ASP.NET Core output-cache middleware to the application pipeline.
    /// </summary>
    /// <param name="app">The application pipeline builder.</param>
    /// <remarks>
    /// The host must insert the Aero CMS module pipeline after routing, authentication, and
    /// authorization so endpoint policy metadata and the authenticated principal are available.
    /// Calling this method makes the registered policies executable; endpoints must still select
    /// a policy to cache responses.
    /// </remarks>
    public void ConfigurePipeline(IApplicationBuilder app)
        => app.UseOutputCache();
}
