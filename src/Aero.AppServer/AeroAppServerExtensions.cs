using Aero.Cms.Web.Core.Modules;
using Aero.Core.Logging;
using Marten;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TickerQ.DependencyInjection;
using Wolverine;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;

namespace Aero.AppServer;

// todo - move the aero.appserver project to its own sln and git repo

public static class AeroAppServerExtensions
{
    public static IHostApplicationBuilder AddAeroApplicationServer(this IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        var config = builder.Configuration;
        var env = builder.Environment;

        builder.AddAeroLogging();

        services.AddHostedService<AeroLifetimeObserver>();
        services.AddHostedService<AeroCacheService>();
        services.AddHostedService<AeroEmbeddedDbService>();

        var connString = config.GetConnectionString("aero") 
            ?? AeroAppServerConstants.EmbedConnString;

        var cacheString = config.GetConnectionString("cache") 
            ?? AeroAppServerConstants.CacheUrl;

        builder.UseOrleans(silo =>
        {
            // This tells Orleans: "Look at the assembly that started this process 
            // and find all Grains and Contracts there."
            
            // same nuget pkg bullshit below - claims it can't find method
            //silo.ConfigureApplicationParts(parts =>
            //{
            //    var entryAssembly = Assembly.GetEntryAssembly();
            //    if (entryAssembly != null)
            //    {
            //        parts.AddApplicationPart(entryAssembly).WithReferences();
            //    }
            //});
        });

        services.AddTickerQ(opts =>
        {
            
        });

        // Marten
        services.AddMarten(opts =>
        {
            opts.Connection(connString);
        });

        _ = services.AddWolverine(c => c.)

        services.AddFusionCacheStackExchangeRedisBackplane(opts =>
        {
            opts.Configuration = cacheString;
        });

        // FusionCache
        services.AddFusionCache() // todo - add abiity to pass in fusion cache options from config / or parameter
            .WithDefaultEntryOptions(new FusionCacheEntryOptions
            {
                Duration = TimeSpan.FromMinutes(5) // todo - make cache expiration default configurable
            })
            // Set Redis as the distributed cache layer
            .WithRegisteredDistributedCache()
            // And use it as a backplane to invalidate L1 memory caches
            .WithBackplane(
                new RedisBackplane(new RedisBackplaneOptions { Configuration = cacheString })
            );

        return builder;
    }
}
