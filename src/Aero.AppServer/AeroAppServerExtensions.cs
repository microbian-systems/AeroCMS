using Aero.Cms.Web.Core.Modules;
using Aero.Core.Logging;
using ImTools;
using Marten;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Reflection;
using TickerQ.DependencyInjection;
using Wolverine;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;
using Orleans.Hosting;

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
            silo.UseLocalhostClustering();
                
        });

        services.AddTickerQ(opts =>
        {

        });

        // Marten
        services.AddMarten(opts =>
        {
            opts.Connection(connString);
        });


        // For IHostApplicationBuilder, use AddWolverine on services
        services.AddWolverine(ExtensionDiscovery.ManualOnly, opts =>
        {
            // 2. This disables the handler conventions (Handle/Consume naming rules)
            opts.Discovery.DisableConventionalDiscovery();

            // 3. Manually scan the AppDomain safely
            var moduleAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic)
                .Where(a =>
                {
                    var name = a.GetName().Name;
                    return name != null &&
                           !name.StartsWith("Microsoft.") &&
                           !name.StartsWith("System.") &&
                           !name.StartsWith("Orleans.") &&
                           !name.StartsWith("Radzen") &&
                           !name.StartsWith("TickerQ") &&
                           !name.StartsWith("Serilog") &&
                           !name.StartsWith("Ziggy") &&
                           !name.StartsWith("StackExchange") &&
                           !name.StartsWith("Npgsql")
                           ;
                })
                .ToList();

            foreach (var assembly in moduleAssemblies)
            {
                try
                {
                    // Only include if it actually implements your marker interface
                    if (assembly.GetTypes().Any(t => typeof(IAeroModule).IsAssignableFrom(t)))
                    {
                        opts.Discovery.IncludeAssembly(assembly);
                    }
                }
                catch (ReflectionTypeLoadException) { /* Skip problematic DLLs */ }
            }

            // 4. Don't forget your Entry Assembly! 
            opts.Discovery.IncludeAssembly(Assembly.GetEntryAssembly()!);

        }); // <--- Use .None instead of .ManualOnly

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
