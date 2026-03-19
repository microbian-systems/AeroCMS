using Aero.Cms.Core.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace Aero.Cms.Modules.Cache;

/// <summary>
/// Infrastructure module for high-performance output caching using FusionCache.
/// </summary>
public class CacheModule : AeroModuleBase
{
    public override string Name => nameof(CacheModule);
    public override string Version => "0.0.5-alpha";
    public override string Author => "Microbians";
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["Infrastructure", "Performance"];
    public override IReadOnlyList<string> Tags => ["cache", "memory", "performance"];

    public override void ConfigureServices(IServiceCollection services)
    {
        // todo - use cache configuration from an options monitor so its updated live when changes are made
        // Register FusionCache with System.Text.Json serializer
        services.AddFusionCache()
            .WithDefaultEntryOptions(options =>
            {
                options.Duration = TimeSpan.FromMinutes(5);
                options.JitterMaxDuration = TimeSpan.FromSeconds(30);
                options.SetFailSafe(true, TimeSpan.FromHours(1));
            })
            .WithSerializer(new FusionCacheSystemTextJsonSerializer());

        // Register the caching hooks
        // services.AddScoped<PageCacheHook>();
        // services.AddScoped<PageCacheStoreHook>();
        // services.AddScoped<PageCacheInvalidatorHook>();
    }

    public override void Run(IEndpointRouteBuilder endpoints)
    {
    }

    public override void Configure(IModuleBuilder builder)
    {
        // Registration with the global hook system will happen here
        // builder.AddPageReadHook<PageCacheHook>();
        // builder.AddPageReadHook<PageCacheStoreHook>();
        // builder.AddPageSaveHook<PageCacheInvalidatorHook>();
    }
}
