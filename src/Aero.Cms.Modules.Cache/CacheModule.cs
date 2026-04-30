using Aero.Cms.Core;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;
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
public class CacheModule : AeroModuleBase
{
    public override string Name => nameof(CacheModule);
    public override string Version => AeroConstants.Version;
    public override string Author => AeroConstants.Author;
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["Infrastructure", "Performance"];
    public override IReadOnlyList<string> Tags => ["cache", "memory", "performance"];

    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        // ---- Distributed cache (L2) ----
        // MemoryDistributedCache is an in-memory IDistributedCache fallback.
        // When an external cache (Redis/Garnet) is configured, replace this with
        // AddStackExchangeRedisCache() in the host's bootstrap config.
        services.AddDistributedMemoryCache();

        // ---- Cache connection string ----
        // Read from bootstrap config. Falls back to Memory mode (no external cache).
        var cacheMode = config?.GetValue<string>("AeroCms:Bootstrap:CacheMode") ?? "Memory";

        string? cacheString = cacheMode switch
        {
            "Embedded" => $"localhost:{config?.GetValue("Aero:Cache:Port", 33333)}",
            _ => null
        };

        // Register Redis backplane in DI if we have a connection string
        if (!string.IsNullOrWhiteSpace(cacheString))
        {
            services.AddFusionCacheStackExchangeRedisBackplane(opts =>
            {
                opts.Configuration = cacheString;
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
        services.AddScoped<PageCacheHook>();
        services.AddScoped<PageCacheStoreHook>();
        services.AddScoped<PageCacheInvalidatorHook>();
    }

    public override void Configure(IAeroModuleBuilder builder)
    {
        // todo - Registration with the global hook system will happen here

        // builder.addpagereadhook<pagecachehook>();
        // builder.addpagereadhook<pagecachestorehook>();
        // builder.addpagesavehook<pagecacheinvalidatorhook>();
    }
}
