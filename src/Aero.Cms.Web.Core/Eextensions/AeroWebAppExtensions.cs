using Aero.Cms.Web.Core.Modules;
using Aero.EfCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
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
    /// Adds Aero CMS services to the web application builder with default arguments.
    /// </summary>
    public static async Task<(WebApplicationBuilder, ReloadableLogger)> AddAeroCmsAsync<T>(this WebApplicationBuilder builder)
        where T : class => await builder.AddAeroCmsAsync<T>([]);

    /// <summary>
    /// Adds Aero CMS services to the web application builder.
    /// </summary>
    public static async Task<(WebApplicationBuilder, ReloadableLogger)> AddAeroCmsAsync<T>(this WebApplicationBuilder builder, string[] args)
        where T : class
    {
        var config = builder.Configuration;
        var services = builder.Services;
        var env = builder.Environment;

        _ = config.AddConfiguration<T>(env);
        var log = await services.ConfigureLogging(config);


        // Register module system services
        services.AddModuleSystemServices();
        // Configure modules using the new discovery/graph services
        await services.AddAeroModulesAsync(config, env);

        // Wire the data layer AFTER modules have registered their IConfigureMarten contributions.
        // AddMarten() auto-discovers all IConfigureMarten registrations from DI, so module-level
        // schema contributions (BlockMartenConfiguration, DocsMartenConfiguration, etc.) all apply.
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
