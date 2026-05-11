using Aero.Core.Logging;
using Aero.AppServer.Startup;
using Aero.Secrets;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TickerQ.DependencyInjection;
using Wolverine;
using Microsoft.AspNetCore.Builder;
using TickerQ.Dashboard.DependencyInjection;
using JasperFx.Events;
using Aero.Marten.Extensions;


namespace Aero.AppServer;

// todo - move the aero.appserver project to its own sln and git repo for max config options. it can also be used as standalone
// library for hosting the core services (orleans, marten, etc.) without the web server if desired.  This will also allow
// us to target netstandard for the library and net10 for the web server project, which is currently not possible with them
// combined in one project.  Can be combined with Aero.Modular (like aero.cms uses)

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
        var env = builder.Environment;

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
            opts.AddDashboard(dashboard =>
            {
                dashboard.SetBasePath("/manager/jobs");
                dashboard.WithBasicAuth("admin", "*strongPassword1"); // TODO: replace tickerq creds with secure credentials and configuration
            });
        });

        services.ConfigureMartenDb(config, env, connString);

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

    public static WebApplication UseAeroApplicationServer(this WebApplication app)
    {
        app.UseTickerQ();
        return app;
    }
}
