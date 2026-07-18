using Aero.Cms.Core;
using Aero.Cms.Modules.Cache.Handlers;
using Aero.Cms.Modules.Cache.Services;
using Aero.Cms.Modules.OutputCache;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;

namespace Aero.Cms.Modules.Cache;

// todo - rename the csproj (+ folder) to CacheBuster to invalidate cache on page updates, and add a separate CacheModule that just provides the caching services and hooks without the invalidation logic. This way users can choose to use the caching without the invalidation if they want, or implement their own invalidation logic.

/// <summary>
/// Infrastructure module for high-performance output caching using FusionCache.
/// Owns FusionCache registration, distributed cache setup, and page caching hooks.
/// </summary>
[Module(nameof(CacheModule))]
public class CacheModule : AeroModuleBase, IAeroPipelineModule
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public override string Name => nameof(CacheModule);
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
public override IReadOnlyList<string> Dependencies => [nameof(OutputCacheModule)];
        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public override IReadOnlyList<string> Category => ["Infrastructure", "Performance"];
        /// <summary>
    /// Gets or sets the Tags.
    /// </summary>
public override IReadOnlyList<string> Tags => ["cache", "memory", "performance"];
        /// <summary>
    /// Gets or sets the Pipeline Order.
    /// </summary>
public int PipelineOrder => 100;

        /// <summary>
    /// ConfigureServices method.
    /// </summary>
public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        services.AddResponseCaching();

        // ---- Cache connection string ----
        // The bootstrap layer resolves secrets and publishes the effective cache
        // endpoint as ConnectionStrings:cache before modules are configured.
        var cacheMode = config?.GetValue<string>("AeroCms:Bootstrap:CacheMode") ?? "Memory";
        var cacheString = config?.GetConnectionString("cache");
        if (string.IsNullOrWhiteSpace(cacheString)
            && cacheMode.Equals("Embedded", StringComparison.OrdinalIgnoreCase))
        {
            cacheString = $"localhost:{config?.GetValue("Aero:Cache:Port", 33333)}";
        }

        // ---- Distributed cache (FusionCache L2) ----
        // Garnet is Redis-protocol compatible. Memory mode deliberately remains
        // process-local; Embedded and Server modes share L2 across web instances.
        if (string.IsNullOrWhiteSpace(cacheString))
        {
            services.AddDistributedMemoryCache();
        }
        else
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = cacheString;
                options.InstanceName = "aero:domain:";
            });
        }

        // ---- FusionCache ----
        var cacheBuilder = services.AddFusionCache()
            .WithDefaultEntryOptions(new FusionCacheEntryOptions
            {
                Duration = TimeSpan.FromMinutes(5)
            })
            .WithSystemTextJsonSerializer()
            .WithRegisteredDistributedCache(ignoreMemoryDistributedCache: false);

        if (!string.IsNullOrWhiteSpace(cacheString))
        {
            cacheBuilder.WithBackplane(new RedisBackplane(new RedisBackplaneOptions
            {
                Configuration = cacheString
            }));
        }

        // ---- Page caching hooks ----
        services.AddSingleton<ICacheInvalidationService, FusionCacheInvalidationService>();
        services.AddScoped<ContentUpdatedHandler>();
        services.AddScoped<PageCacheHook>();
        services.AddScoped<PageCacheStoreHook>();
        services.AddScoped<PageCacheInvalidatorHook>();
    }

        /// <summary>
    /// ConfigurePipeline method.
    /// </summary>
public void ConfigurePipeline(IApplicationBuilder app)
    {
        app.Use(async (context, next) =>
        {
            if (IsManagerOrAdminPath(context.Request.Path))
            {
                context.Response.OnStarting(() =>
                {
                    context.Response.Headers.CacheControl = "no-store, no-cache";
                    context.Response.Headers.Pragma = "no-cache";
                    context.Response.Headers.Expires = "0";
                    return Task.CompletedTask;
                });
            }

            await next();
        });

        app.UseResponseCaching();
    }

        /// <summary>
    /// Configure method.
    /// </summary>
public override void Configure(IAeroModuleBuilder builder)
    {
        // todo - Registration with the global hook system will happen here

        // builder.addpagereadhook<pagecachehook>();
        // builder.addpagereadhook<pagecachestorehook>();
        // builder.addpagesavehook<pagecacheinvalidatorhook>();
    }

    private static bool IsManagerOrAdminPath(PathString path)
        => path.StartsWithSegments("/manager", StringComparison.OrdinalIgnoreCase) ||
           path.StartsWithSegments("/admin", StringComparison.OrdinalIgnoreCase) ||
           path.StartsWithSegments("/api/v1/admin", StringComparison.OrdinalIgnoreCase);
}
