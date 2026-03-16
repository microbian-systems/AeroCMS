using Aero.Cms.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace Aero.Cms.Modules.Cache;

/// <summary>
/// Infrastructure module for high-performance output caching using FusionCache.
/// </summary>
public class CacheModule : AeroModuleBase
{
    public override string Name=> "Caching";
    public override string Version => "1.0.0";
    public override string Author => "Microbian Systems";
    public override IReadOnlyList<string> Dependencies => [];
    public override string Description => "A content caching module for in AeroCMS";

    public override void ConfigureServices(IServiceCollection services, IConfiguration config = null)
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
        services.AddScoped<PageCacheHook>();
        services.AddScoped<PageCacheStoreHook>();
        services.AddScoped<PageCacheInvalidatorHook>();
    }

    public override void Init(IServiceProvider sp)
    {
        
    }

    public override void Run(IEndpointRouteBuilder app)
    {
        
    }

    public override Task RunAsync(IEndpointRouteBuilder app)
    {
        return Task.CompletedTask;
    }

    public override void Configure(IAeroModuleBuilder builder)
    {
        // Registration with the global hook system will happen here
        // builder.AddPageReadHook<PageCacheHook>();
        // builder.AddPageReadHook<PageCacheStoreHook>();
        // builder.AddPageSaveHook<PageCacheInvalidatorHook>();
    }
}
