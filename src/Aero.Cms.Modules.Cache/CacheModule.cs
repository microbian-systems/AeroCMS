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
/// Registers FusionCache, Redis-backed distributed cache services, cache invalidation services, and response-caching middleware.
/// </summary>
/// <remarks>
/// FusionCache is configured with a five-minute default entry duration, the registered distributed cache, and a
/// Redis backplane using the selected cache connection. These registrations alone do not prove cross-node coherence
/// or transactional invalidation. The page hook types are registered only as concrete scoped services; this module's
/// <see cref="Configure"/> contains no global hook-pipeline wiring, so page read/store/save hooks are inactive.
/// </remarks>
[Module(nameof(CacheModule))]
public class CacheModule : AeroModuleBase, IAeroPipelineModule
{
    /// <summary>Gets the module's fixed discovery name.</summary>
public override string Name => nameof(CacheModule);
    /// <summary>Gets the Aero CMS version reported by this module.</summary>
public override string Version => AeroConstants.Version;
    /// <summary>Gets the Aero CMS author metadata reported by this module.</summary>
public override string Author => AeroConstants.Author;
    /// <summary>Gets the required Output Cache module dependency.</summary>
public override IReadOnlyList<string> Dependencies => [nameof(OutputCacheModule)];
    /// <summary>Gets module-discovery categories.</summary>
public override IReadOnlyList<string> Category => ["Infrastructure", "Performance"];
    /// <summary>Gets module-discovery tags.</summary>
public override IReadOnlyList<string> Tags => ["cache", "memory", "performance"];
    /// <summary>Gets the module middleware-pipeline ordering value.</summary>
public int PipelineOrder => 100;

        /// <summary>
    /// Registers cache services and the concrete (but inactive) page-hook types.
    /// </summary>
public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        services.AddResponseCaching();

        // ---- Cache connection string ----
        // The bootstrap layer resolves secrets and publishes the effective cache
        // endpoint as ConnectionStrings:cache before modules are configured.
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

        // ---- Distributed cache (FusionCache L2) ----
        // Local mode points at the in-process Garnet server; Server mode points
        // at the configured remote Redis-compatible endpoint.
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = cacheString;
            options.InstanceName = "aero:domain:";
        });

        // ---- FusionCache ----
        var cacheBuilder = services.AddFusionCache()
            .WithDefaultEntryOptions(new FusionCacheEntryOptions
            {
                Duration = TimeSpan.FromMinutes(5)
            })
            .WithSystemTextJsonSerializer()
            .WithRegisteredDistributedCache(ignoreMemoryDistributedCache: false);

        cacheBuilder.WithBackplane(new RedisBackplane(new RedisBackplaneOptions
        {
            Configuration = cacheString
        }));

        // ---- Page caching hooks ----
        services.AddSingleton<ICacheInvalidationService, FusionCacheInvalidationService>();
        services.AddScoped<ContentUpdatedHandler>();
        services.AddScoped<PageCacheHook>();
        services.AddScoped<PageCacheStoreHook>();
        services.AddScoped<PageCacheInvalidatorHook>();
    }

        /// <summary>
    /// Adds response-caching middleware and prevents cache headers on manager and admin paths.
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
    /// Performs no page-hook registration in the global hook pipeline.
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
