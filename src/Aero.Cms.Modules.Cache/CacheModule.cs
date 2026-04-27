using Aero.Cms.Core;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.Cache;

// todo - rename the csproj (+ folder) to CacheBuster to invalidate cache on page updates, and add a separate CacheModule that just provides the caching services and hooks without the invalidation logic. This way users can choose to use the caching without the invalidation if they want, or implement their own invalidation logic.

/// <summary>
/// Infrastructure module for high-performance output caching using FusionCache.
/// </summary>
public class CacheBusterModule : AeroModuleBase
{
    public override string Name => nameof(CacheBusterModule);
    public override string Version => AeroConstants.Version;
    public override string Author => AeroConstants.Author;
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["Infrastructure", "Performance"];
    public override IReadOnlyList<string> Tags => ["cache", "memory", "performance"];

    public override void ConfigureServices(IServiceCollection services, IConfiguration? config=null, IHostEnvironment? env=null)
    {
        // Register FusionCache with System.Text.Json serializer
        /******* thisis now configured in the Aero.AppServer project *******/
        //services.AddFusionCache()
        //    .WithDefaultEntryOptions(options =>
        //    {
        //        options.Duration = TimeSpan.FromMinutes(5);
        //        options.JitterMaxDuration = TimeSpan.FromSeconds(30);
        //        options.SetFailSafe(true, TimeSpan.FromHours(1));
        //    })
        //    .WithSerializer(new FusionCacheSystemTextJsonSerializer());

        // Register the caching hooks
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
