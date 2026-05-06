using Aero.Core.Logging;
using Aero.AppServer.Startup;
using Aero.Secrets;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TickerQ.DependencyInjection;
using Wolverine;


namespace Aero.AppServer;

// todo - move the aero.appserver project to its own sln and git repo

public static class AeroAppServerExtensions
{
    /// <summary>
    /// Adds Aero application server services (Orleans, Marten, TickerQ, Wolverine).
    /// Wolverine handler discovery is driven by the source-generated
    /// <c>GeneratedWolverineHandlerCatalog.Register</c> callback.
    /// No AppDomain assembly scanning is performed.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configureWolverine">
    /// Optional callback to configure Wolverine options.
    /// The main host call site passes <c>GeneratedWolverineHandlerCatalog.Register</c>,
    /// which disables conventional discovery and registers only the source-generated
    /// handler types via explicit <c>IncludeType&lt;T&gt;()</c> calls.
    /// When null, conventional discovery is disabled and no handlers are registered.
    /// </param>
    public static Task<IHostApplicationBuilder> AddAeroApplicationServer(
        this IHostApplicationBuilder builder,
        Action<WolverineOptions>? configureWolverine = null)
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
        })
        .UseLightweightSessions();

        // Wolverine — handler discovery is driven by the source-generated
        // GeneratedWolverineHandlerCatalog callback, which disables conventional
        // discovery and includes only explicitly registered handler types.
        // When no callback is provided, conventional discovery is still disabled
        // and no handler scanning occurs (safe default: empty handler set).
        services.AddWolverine(ExtensionDiscovery.ManualOnly, opts =>
        {
            opts.Discovery.DisableConventionalDiscovery();
            configureWolverine?.Invoke(opts);
        });

        return Task.FromResult(builder);
    }
}
