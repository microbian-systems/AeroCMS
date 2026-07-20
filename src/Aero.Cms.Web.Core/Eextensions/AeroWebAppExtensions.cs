using Aero.Cms.Web.Core.Modules;
using Aero.EfCore.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog.Extensions.Hosting;
using Aero.Core.Extensions;
using Aero.Cms.Core.Extensions;
using Aero.Cms.Modules.Modules.Services;
using Aero.Modular;

namespace Aero.Cms.Web.Core.Eextensions;

/// <summary>
/// Provides host integration for Aero CMS bootstrap, runtime services, module startup, endpoints, and middleware.
/// </summary>
/// <remarks>
/// Registration methods do not automatically initialize modules, map endpoints, or add module middleware. The host
/// must call the corresponding methods at the intended lifecycle points. This class applies no global authorization
/// policy to endpoints contributed by modules.
/// </remarks>
public static class AeroWebAppExtensions
{
    /// <summary>
    /// Adds bootstrap-safe Aero CMS services to the web application builder with default arguments.
    /// </summary>
    /// <typeparam name="T">The configuration marker type used by Aero configuration extensions.</typeparam>
    /// <param name="builder">The web application builder to configure.</param>
    /// <returns>The same builder and the reloadable logger created by logging configuration.</returns>
    public static async Task<(WebApplicationBuilder, ReloadableLogger)> AddAeroCmsBootstrapAsync<T>(
        this WebApplicationBuilder builder)
        where T : class => await builder.AddAeroCmsBootstrapAsync<T>([]);

    /// <summary>
    /// Adds bootstrap-safe Aero CMS services to the web application builder.
    /// </summary>
    /// <typeparam name="T">The configuration marker type used by Aero configuration extensions.</typeparam>
    /// <param name="builder">The web application builder to configure.</param>
    /// <param name="args">Arguments forwarded to this overload; the current implementation does not otherwise consume them.</param>
    /// <returns>The same builder and the reloadable logger created by logging configuration.</returns>
    /// <remarks>Configuration and logging failures propagate; this method does not register the CMS data or module runtime.</remarks>
    public static async Task<(WebApplicationBuilder, ReloadableLogger)> AddAeroCmsBootstrapAsync<T>(
        this WebApplicationBuilder builder, string[] args)
        where T : class
    {
        var config = builder.Configuration;
        var services = builder.Services;
        var env = builder.Environment;

        _ = config.AddConfiguration<T>(env);
        var log = await services.ConfigureLogging(config);


        return (builder, log);
    }

        /// <summary>
    /// Adds Aero configuration/logging, generated module registrations, module-system services, and the Aero data layer.
    /// </summary>
    /// <typeparam name="T">The configuration marker type used by Aero configuration extensions.</typeparam>
    /// <param name="builder">The web application builder to configure.</param>
    /// <param name="generatedDescriptors">Source-generated module descriptors passed to module registration.</param>
    /// <param name="args">Optional arguments normalized to an empty array but otherwise unused by this implementation.</param>
    /// <param name="configureResolvedInfrastructure">Optional callback invoked after base configuration and before logging/services.</param>
    /// <returns>The same builder and configured reloadable logger.</returns>
    /// <remarks>
    /// This method registers services only; it does not initialize modules, map endpoints, or add middleware.
    /// Callback, logging, module-registration, and data-layer failures propagate.
    /// </remarks>
public static async Task<(WebApplicationBuilder, ReloadableLogger)> AddAeroCmsRuntimeAsync<T>(
        this WebApplicationBuilder builder,
        IReadOnlyList<ModuleDescriptor> generatedDescriptors,
        string[]? args = null,
        Action<ConfigurationManager>? configureResolvedInfrastructure = null)
        where T : class
    {
        args ??= [];
        var config = builder.Configuration;
        var services = builder.Services;
        var env = builder.Environment;

        _ = config.AddConfiguration<T>(env);
        configureResolvedInfrastructure?.Invoke(config);
        var log = await services.ConfigureLogging(config);

        services.AddModuleSystemServices();
        await services.AddAeroModulesAsync(config, env, generatedDescriptors);
        services.AddAeroDataLayer(config, env);

        return (builder, log);
    }

    /// <summary>
    /// Evaluates bootstrap state and skips runtime preparation while the application is in Setup state.
    /// </summary>
    /// <param name="endpoints">The route builder whose service provider supplies configuration and logging.</param>
    /// <returns>The supplied <paramref name="endpoints"/>.</returns>
    /// <remarks>
    /// The current Sable implementation applies no database migration or persistence operation. Setup state is read
    /// from <c>AeroCms:Bootstrap:State</c>, with legacy completion flags used as a fallback. The created async scope is
    /// not disposed by this method. Resolution and logging failures propagate; no cancellation token is exposed.
    /// </remarks>
    public static async Task<IEndpointRouteBuilder> PrepareAeroAppAsync(
        this IEndpointRouteBuilder endpoints)
    {
        var scope = endpoints.ServiceProvider.CreateAsyncScope();
        var services = scope.ServiceProvider;

        var configuration = services.GetRequiredService<IConfiguration>();
        var bootstrapSection = configuration.GetSection("AeroCms:Bootstrap");
        var state = bootstrapSection["State"];
        if (string.IsNullOrWhiteSpace(state))
        {
            var setupComplete = bootstrapSection.GetValue<bool?>("SetupComplete") ?? false;
            var seedComplete = bootstrapSection.GetValue<bool?>("SeedComplete") ?? false;
            state = setupComplete && seedComplete ? "Running" : bootstrapSection.Exists() ? "Configured" : "Setup";
        }

        if (string.Equals(state, "Setup", StringComparison.OrdinalIgnoreCase))
        {
            var loggerFactory = services.GetRequiredService<ILoggerFactory>();
            var startupLogger = loggerFactory.CreateLogger("AeroStartup");
            startupLogger.LogInformation(
                "Bootstrap mode detected. Skipping database migrations and module runtime initialization so the setup page can run first.");
            return endpoints;
        }

        // EF Core Npgsql migrations removed.
        // All persistence now handled by AeroDB.Sable (IDocumentSession).

        return endpoints;
    }

    /// <summary>
    /// Initializes module runtime services in dependency order.
    /// </summary>
    /// <param name="endpoints">The route builder whose root provider supplies modules and configuration.</param>
    /// <returns>The supplied <paramref name="endpoints"/> after all selected module startup tasks complete.</returns>
    /// <remarks>
    /// Setup state skips initialization. Otherwise the module graph load order is preferred; without a graph,
    /// registered modules are ordered by <c>Order</c>. Execution is sequential, has no cancellation token or rollback,
    /// and stops on the first propagated failure. The created async scope is not disposed by this method, while module
    /// resolution itself uses the root endpoint service provider.
    /// </remarks>
    public static async Task<IEndpointRouteBuilder> InitializeAeroAppAsync(
        this IEndpointRouteBuilder endpoints)
    {
        var scope = endpoints.ServiceProvider.CreateAsyncScope();
        var services = scope.ServiceProvider;

        var configuration = services.GetRequiredService<IConfiguration>();
        var bootstrapSection = configuration.GetSection("AeroCms:Bootstrap");
        var state = bootstrapSection["State"];
        if (string.IsNullOrWhiteSpace(state))
        {
            var setupComplete = bootstrapSection.GetValue<bool?>("SetupComplete") ?? false;
            var seedComplete = bootstrapSection.GetValue<bool?>("SeedComplete") ?? false;
            state = setupComplete && seedComplete ? "Running" : bootstrapSection.Exists() ? "Configured" : "Setup";
        }

        if (string.Equals(state, "Setup", StringComparison.OrdinalIgnoreCase))
        {
            var loggerFactory = services.GetRequiredService<ILoggerFactory>();
            var startupLogger = loggerFactory.CreateLogger("AeroStartup");
            startupLogger.LogInformation(
                "Bootstrap mode detected. Skipping module runtime initialization so the setup page can run first.");
            return endpoints;
        }

        var graph = endpoints.ServiceProvider.GetService<ModuleGraph>();

        if (graph != null)
        {
            // Use the graph's load order if available
            foreach (var descriptor in graph.LoadOrder)
            {
                var module = endpoints.ServiceProvider.GetService(descriptor.ModuleType) as IAeroModule;
                if (module != null)
                {
                    await module.RunAsync(services);
                }
            }
        }
        else
        {
            // todo - verify this gets the setup module first so the graph is built before other modules run
            // Fallback: use traditional ordering
            var modules = endpoints.ServiceProvider
                .GetServices<IAeroModule>()
                .OrderBy(m => m.Order)
                .ToList();

            foreach (var module in modules)
            {
                await module.RunAsync(services);
            }
        }

        return endpoints;
    }

    /// <summary>
    /// Maps Aero module endpoints in dependency order.
    /// </summary>
    /// <param name="endpoints">The route builder passed to each resolved web module.</param>
    /// <returns>The supplied <paramref name="endpoints"/>.</returns>
    /// <remarks>
    /// Graph load order is preferred; fallback modules are ordered by <c>Order</c>. Each module owns its route,
    /// serialization, authentication, and authorization metadata. Mapping is synchronous and stops on a propagated
    /// module failure.
    /// </remarks>
    public static IEndpointRouteBuilder MapAeroCmsEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var graph = endpoints.ServiceProvider.GetService<ModuleGraph>();

        if (graph != null)
        {
            // Use the graph's load order if available
            foreach (var descriptor in graph.LoadOrder)
            {
                var module = endpoints.ServiceProvider.GetService(descriptor.ModuleType) as IAeroWebModule;
                module?.Run(endpoints);
            }
        }
        else
        {
            // Fallback: use traditional ordering
            var modules = endpoints.ServiceProvider
                .GetServices<IAeroWebModule>()
                .OrderBy(m => m.Order)
                .ToList();

            foreach (var module in modules)
            {
                module.Run(endpoints);
            }
        }

        return endpoints;
    }

    /// <summary>
    /// Applies middleware contributed by Aero CMS modules in explicit pipeline order.
    /// The host chooses the insertion point; modules own their middleware details.
    /// </summary>
    /// <param name="app">The application builder to which module middleware is added.</param>
    /// <returns>The supplied <paramref name="app"/>.</returns>
    /// <remarks>
    /// Pipeline modules are ordered first by <see cref="IAeroPipelineModule.PipelineOrder"/> and then module
    /// <c>Order</c>. The graph controls discovery order when present. Configuration is synchronous and failures
    /// propagate; this method does not add authentication, exception handling, or routing by itself.
    /// </remarks>
    public static IApplicationBuilder UseAeroCmsModulePipeline(
        this IApplicationBuilder app)
    {
        var graph = app.ApplicationServices.GetService<ModuleGraph>();

        var modules = graph is not null
            ? graph.LoadOrder
                .Select(descriptor => app.ApplicationServices.GetService(descriptor.ModuleType))
                .OfType<IAeroPipelineModule>()
                .ToList()
            : app.ApplicationServices
                .GetServices<IAeroModule>()
                .OfType<IAeroPipelineModule>()
                .ToList();

        foreach (var module in modules
                     .OrderBy(module => module.PipelineOrder)
                     .ThenBy(module => module.Order))
        {
            module.ConfigurePipeline(app);
        }

        return app;
    }
}
