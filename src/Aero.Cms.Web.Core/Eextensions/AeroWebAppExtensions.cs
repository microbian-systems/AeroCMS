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
using Aero.Cms.Web.Core.Blocks.Rendering;
using Aero.Modular;
using Microsoft.AspNetCore.Components.Web;

namespace Aero.Cms.Web.Core.Eextensions;

/// <summary>
/// Represents a class for AeroWebAppExtensions.
/// </summary>
public static class AeroWebAppExtensions
{
    /// <summary>
    /// Adds bootstrap-safe Aero CMS services to the web application builder with default arguments.
    /// </summary>
    public static async Task<(WebApplicationBuilder, ReloadableLogger)> AddAeroCmsBootstrapAsync<T>(
        this WebApplicationBuilder builder)
        where T : class => await builder.AddAeroCmsBootstrapAsync<T>([]);

    /// <summary>
    /// Adds bootstrap-safe Aero CMS services to the web application builder.
    /// </summary>
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
    /// AddAeroCmsRuntimeAsync method.
    /// </summary>
public static async Task<(WebApplicationBuilder, ReloadableLogger)> AddAeroCmsRuntimeAsync<T>(
        this WebApplicationBuilder builder,
        IReadOnlyList<ModuleDescriptor> generatedDescriptors,
        string[]? args = null)
        where T : class
    {
        args ??= [];
        var config = builder.Configuration;
        var services = builder.Services;
        var env = builder.Environment;

        _ = config.AddConfiguration<T>(env);
        var log = await services.ConfigureLogging(config);

        services.AddBlockSystemServices();
        services.AddScoped<HtmlRenderer>();
        services.AddScoped<CmsBlockHtmlRenderer>();
        services.AddModuleSystemServices();
        await services.AddAeroModulesAsync(config, env, generatedDescriptors);
        services.AddAeroDataLayer(config, env);

        return (builder, log);
    }

    /// <summary>
    /// Applies database migrations and other runtime preparation.
    /// </summary>
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
