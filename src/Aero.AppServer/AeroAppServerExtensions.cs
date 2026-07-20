using Aero.Core.Logging;
using Aero.AppServer.Startup;
using Aero.Secrets;
using Aero.Models.Entities;
using AeroDB.Sable;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SurrealDb.Embedded.SurrealKv;
using TickerQ.Dashboard.DependencyInjection;
using TickerQ.DependencyInjection;
using Wolverine;


namespace Aero.AppServer;

// todo - move the aero.appserver project to its own sln and git repo for max config options. it can also be used as standalone
// library for hosting the core services (orleans, marten, etc.) without the web server if desired.  This will also allow
// us to target netstandard for the library and net10 for the web server project, which is currently not possible with them
// combined in one project.  Can be combined with Aero.Modular (like aero.cms uses)

/// <summary>
/// Provides host registration and middleware extensions for AeroCMS infrastructure.
/// </summary>
public static class AeroAppServerExtensions
{
    /// <summary>
    /// Adds Aero application server services (Orleans, AeroDB, TickerQ, Wolverine).
    /// Wolverine handler and grain assembly discovery are driven by source-generated
    /// catalogs passed as callbacks — no AppDomain assembly scanning is performed.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configureWolverine">
    /// Optional callback to configure Wolverine options.
    /// The main host call site passes <c>GeneratedWolverineHandlerCatalog.Register</c>,
    /// which disables conventional discovery and registers only the source-generated
    /// handler types via explicit <c>IncludeType&lt;T&gt;()</c> calls.
    /// When null, conventional discovery is disabled and no handlers are registered.
    /// </param>
    /// <param name="configureGrains">
    /// Optional callback to configure the Orleans silo builder for grain assembly
    /// registration. The host passes a callback that adds each module's grain
    /// assembly via <c>ISiloBuilder.ConfigureApplicationParts()</c>.
    /// Mirrors the Wolverine callback pattern. When null, this method does not
    /// register any additional module grain assemblies.
    /// </param>
    /// <returns>The same builder after infrastructure registrations have been added.</returns>
    /// <remarks>
    /// This method resolves bootstrap secrets immediately, creates the embedded database directory
    /// when selected, and registers local database/cache hosted services conditionally. The built-in
    /// TickerQ dashboard currently uses hard-coded basic-auth credentials; hosts must not expose it
    /// beyond a trusted boundary until that existing configuration is replaced.
    /// </remarks>
    public static Task<IHostApplicationBuilder> AddAeroApplicationServer(
        this IHostApplicationBuilder builder,
        Action<WolverineOptions>? configureWolverine = null,
        Action<ISiloBuilder>? configureGrains = null)
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

        if (resolved.CacheMode.Equals(AeroAppServerConstants.LocalCacheMode, StringComparison.OrdinalIgnoreCase))
        {
            services.AddHostedService<AeroCacheService>();
        }


        services.AddOrleans(opts =>
        {
            opts.UseLocalhostClustering();
            configureGrains?.Invoke(opts);
        });

        services.AddTickerQ(opts =>
        {
            opts.AddDashboard(dashboard =>
            {
                dashboard.SetBasePath("/manager/jobs");
                dashboard.WithBasicAuth("admin", "*strongPassword1"); // TODO: replace tickerq creds with secure credentials and configuration
            });
        });

        // Register AeroDB (SurrealDB) document store
        services.AddAeroDB(opts =>
        {
            opts.Namespace = "aero";
            opts.Database = "aero";

            if (resolved.DatabaseMode.Equals("Embedded", StringComparison.OrdinalIgnoreCase))
            {
                var dataPath = Path.Combine(env.ContentRootPath, "App_Data", "aerodb-surrealkv")
                    .Replace(Path.DirectorySeparatorChar, '/');
                Directory.CreateDirectory(dataPath);
                opts.ClientFactory = () => new SurrealDbKvClient(dataPath);
            }
            else
            {
                opts.Endpoint = resolved.DatabaseConnectionString;
                if (!resolved.DatabaseUnauthenticated)
                {
                    opts.Username = resolved.DatabaseUsername;
                    opts.Password = resolved.DatabasePassword;
                }
            }

            // Schema configuration
            opts.Schema.For<AeroRole>().Identity(x => x.Id);
            opts.Schema.For<AeroUser>().Identity(x => x.Id);
            // Enable event sourcing with string stream IDs
            opts.Events.StreamIdentity = StreamIdentity.AsString;
        });

        // Wolverine — handler discovery is driven by the source-generated
        // GeneratedWolverineHandlerCatalog callback, which disables conventional
        // discovery and includes only explicitly registered handler types.
        // When no callback is provided, conventional discovery is still disabled
        // and no handler scanning occurs (safe default: empty handler set).
        services.AddWolverine(ExtensionDiscovery.ManualOnly, opts =>
        {
            opts.UseRuntimeCompilation();
            opts.Discovery.DisableConventionalDiscovery();
            configureWolverine?.Invoke(opts);
        });

        return Task.FromResult(builder);
    }

    /// <summary>
    /// Adds the TickerQ dashboard middleware to the application pipeline.
    /// </summary>
    /// <param name="app">The application whose middleware pipeline is being configured.</param>
    /// <returns>The same application instance for further pipeline configuration.</returns>
public static WebApplication UseAeroApplicationServer(this WebApplication app)
    {
        app.UseTickerQ();
        return app;
    }
}
