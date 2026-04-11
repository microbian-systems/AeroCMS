using Aero.Cms.Web.Core.Modules;
using Aero.EfCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog.Extensions.Hosting;
using Aero.EfCore.Extensions;
using Aero.Core.Extensions;
using Aero.Cms.Core.Extensions;

namespace Aero.Cms.Web.Core.Eextensions;

public static class AeroWebAppExtensions
{
    /// <summary>
    /// Adds bootstrap-safe Aero CMS services to the web application builder with default arguments.
    /// </summary>
    public static async Task<(WebApplicationBuilder, ReloadableLogger)> AddAeroCmsBootstrapAsync<T>(this WebApplicationBuilder builder)
        where T : class => await builder.AddAeroCmsBootstrapAsync<T>([]);

    /// <summary>
    /// Adds bootstrap-safe Aero CMS services to the web application builder.
    /// </summary>
    public static async Task<(WebApplicationBuilder, ReloadableLogger)> AddAeroCmsBootstrapAsync<T>(this WebApplicationBuilder builder, string[] args)
        where T : class
    {
        var config = builder.Configuration;
        var services = builder.Services;
        var env = builder.Environment;

        _ = config.AddConfiguration<T>(env);
        var log = await services.ConfigureLogging(config);


        return (builder, log);
    }

    public static async Task<(WebApplicationBuilder, ReloadableLogger)> AddAeroCmsRuntimeAsync<T>(this WebApplicationBuilder builder)
        where T : class => await builder.AddAeroCmsRuntimeAsync<T>([]);

    public static async Task<(WebApplicationBuilder, ReloadableLogger)> AddAeroCmsRuntimeAsync<T>(this WebApplicationBuilder builder, string[] args)
        where T : class
    {
        var config = builder.Configuration;
        var services = builder.Services;
        var env = builder.Environment;

        _ = config.AddConfiguration<T>(env);
        var log = await services.ConfigureLogging(config);

        services.AddModuleSystemServices();
        await services.AddAeroModulesAsync(config, env);
        services.AddAeroDataLayer(config, env);

        return (builder, log);
    }

    public static IApplicationBuilder UseAeroCmsModules(this IApplicationBuilder app)
    {
        if (app is IEndpointRouteBuilder endpoints)
        {
            endpoints.MapAeroCmsEndpoints();
        }

        return app;
    }

    /// <summary>
    /// Initializes the Aero application asynchronously in dependency order.
    /// </summary>
    public static async Task<IEndpointRouteBuilder> MapAeroAppAsync(
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
            startupLogger.LogInformation("Bootstrap mode detected. Skipping database migrations and module runtime initialization so the setup page can run first.");
            return endpoints;
        }

        var apiContext = services.GetRequiredService<AeroApiContext>();
        var created = await apiContext.Database.EnsureCreatedAsync();
        await apiContext.Database.MigrateAsync();

        var dbContext = services.GetRequiredService<AeroDbContext>();
        await dbContext.Database.MigrateAsync();

        // Optional: Log success
        var factory = services.GetRequiredService<ILoggerFactory>();
        var logger = factory.CreateLogger<AeroDbContext>();
        logger.LogInformation("Database migrations applied successfully");

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
                    if(module is IAeroWebModule webModule)
                        await webModule.RunAsync(endpoints);
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
                if (module is IAeroWebModule webModule)
                    await webModule.RunAsync(endpoints);
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
}
