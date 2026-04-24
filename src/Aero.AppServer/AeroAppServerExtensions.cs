using Aero.Cms.Web.Core.Modules;
using Aero.Core.Logging;
using Aero.AppServer.Startup;
using Aero.Secrets;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Reflection;
using TickerQ.DependencyInjection;
using Wolverine;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;

namespace Aero.AppServer;

// todo - move the aero.appserver project to its own sln and git repo

public static class AeroAppServerExtensions
{
    public static Task<IHostApplicationBuilder> AddAeroApplicationServer(this IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        var config = builder.Configuration;

        builder.AddAeroLogging();

        services.AddOptions<AeroDbOptions>()
            .BindConfiguration("Aero:Embedded");

        services.AddHostedService<AeroLifetimeObserver>();
        services.AddSingleton<IInfrastructureReadinessSnapshot, InfrastructureReadinessSnapshot>();
        services.AddSingleton<IMultiStartupSignal, MultiStartupSignal>();
        services.AddSingleton<IRuntimeStartupCoordinator, RuntimeStartupCoordinator>();
        services.AddSingleton(DataProtectionCertificateBootstrapper.ResolveSettings(config));
        services.AddSingleton<ISecretManager>(_ => DataProtectionCertificateBootstrapper.CreateSecretManager(config));

        var resolver = new InfrastructureConnectionStringResolver(config);
        var resolved = resolver.Resolve();
        services.AddSingleton(resolved);

        if (resolved.DatabaseMode.Equals("Embedded", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHostedService<AeroEmbeddedDbService>();
        }

        if (resolved.CacheMode.Equals("Embedded", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHostedService<AeroCacheService>();
        }

        var connString = resolved.DatabaseConnectionString;
        var cacheString = resolved.CacheConnectionString;

        services.AddOrleans(opts =>
        {
            opts.UseLocalhostClustering();
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
                    if (assembly.GetTypes().Any(@type => typeof(IAeroModule).IsAssignableFrom(@type)))
                    {
                        opts.Discovery.IncludeAssembly(assembly);
                    }
                }
                catch (ReflectionTypeLoadException) { /* Skip problematic DLLs */ }
            }

            // 4. Don't forget entry assembly! 
            opts.Discovery.IncludeAssembly(Assembly.GetEntryAssembly()!);

        }); // <--- Use .None instead of .ManualOnly

        if (!string.IsNullOrWhiteSpace(cacheString))
        {
            services.AddFusionCacheStackExchangeRedisBackplane(opts =>
            {
                opts.Configuration = cacheString;
            });
        }

        // FusionCache
        var cacheBuilder = services.AddFusionCache() // todo - add abiity to pass in fusion cache options from config / or parameter
            .WithDefaultEntryOptions(new FusionCacheEntryOptions
            {
                Duration = TimeSpan.FromMinutes(5) // todo - make cache expiration default configurable
            })
            // Set Redis as the distributed cache layer
            .WithRegisteredDistributedCache();

        if (!string.IsNullOrWhiteSpace(cacheString))
        {
            // And use it as a backplane to invalidate L1 memory caches
            cacheBuilder.WithBackplane(new RedisBackplane(new RedisBackplaneOptions { Configuration = cacheString }));
        }

        return Task.FromResult(builder);
    }
}
