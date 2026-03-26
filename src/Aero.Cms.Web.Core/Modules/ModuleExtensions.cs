namespace Aero.Cms.Web.Core.Modules;

using Aero.Cms.Core.Blocks;
using Aero.Cms.Core.Modules;
using Aero.Cms.Web.Core.Blocks;
using Aero.Core.Extensions;
using Aero.EfCore;
using Aero.EfCore.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog.Extensions.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Marten;

// todo - abstract/extract Aero modules into its own lib so it can be used in any type of app (host, console, web, etc)

/// <summary>
/// Extension methods for module registration and initialization in ASP.NET Core applications.
/// </summary>
public static class ModuleExtensions
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

        services.AddAeroDataLayer(config, env);

        // Register module system services
        services.AddModuleSystemServices();

        // Configure modules using the new discovery/graph services
        await services.AddAeroModulesAsync(config, env);

        return (builder, log);
    }

    /// <summary>
    /// Registers core module system services (discovery, graph, etc.)
    /// </summary>
    public static IServiceCollection AddModuleSystemServices(this IServiceCollection services)
    {
        // Register block service
        services.TryAddScoped<Aero.Cms.Core.Blocks.IBlockService, Aero.Cms.Web.Core.Blocks.MartenBlockService>();
        services.AddSingleton<global::Marten.IConfigureMarten, Aero.Cms.Web.Core.Blocks.BlockMartenConfiguration>();

        // Register discovery service
        services.TryAddScoped<IModuleDiscoveryService, ModuleDiscoveryService>();

        // Register graph service
        services.TryAddScoped<IModuleGraphService, ModuleGraphService>();

        // Register options
        services.AddOptions<ModuleDiscoveryOptions>();
        services.AddOptions<ModuleGraphOptions>();

        return services;
    }

    /// <summary>
    /// Synchronous wrapper for adding Aero modules.
    /// </summary>
    public static IServiceCollection AddAeroModules(
        this IServiceCollection services,
        IConfiguration config,
        IHostEnvironment env) => services.AddAeroModulesAsync(config, env).GetAwaiter().GetResult();

    public static IServiceCollection AddAeroCmsModules(
        this IServiceCollection services,
        IConfiguration config,
        IHostEnvironment env) => services.AddAeroModules(config, env);

    /// <summary>
    /// Discovers, validates, and registers Aero modules in dependency order.
    /// </summary>
    public static async Task<IServiceCollection> AddAeroModulesAsync(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // Use local service provider for discovery phase to avoid temporary provider issues
        var discoveryServices = new ServiceCollection();
        discoveryServices.AddSingleton(environment);
        discoveryServices.AddLogging();
        discoveryServices.AddOptions();
        discoveryServices.AddModuleSystemServices();

        // Add discovery options from configuration
        discoveryServices.Configure<ModuleDiscoveryOptions>(configuration.GetSection("ModuleDiscovery"));

        await using var discoveryProvider = discoveryServices.BuildServiceProvider();
        using var scope = discoveryProvider.CreateScope();
        var discoveryService = scope.ServiceProvider.GetRequiredService<IModuleDiscoveryService>();
        var graphService = scope.ServiceProvider.GetRequiredService<IModuleGraphService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Aero.Cms.Modules.Startup");

        // Discover modules
        var descriptors = await discoveryService.DiscoverAsync();

        logger.LogInformation("Discovered {ModuleCount} Aero modules: {ModuleNames}",
            descriptors.Count,
            string.Join(", ", descriptors.Select(descriptor => descriptor.Name).OrderBy(name => name)));

        if (descriptors.Count == 0)
        {
            // No modules discovered - register empty module set
            logger.LogWarning("No Aero modules were discovered. Module registration will be skipped.");
            return services;
        }

        // Validate modules before building graph
        var validation = graphService.Validate(descriptors);
        if (!validation.IsValid)
        {
            var error = validation.Errors.First();
            throw new ModuleSystemStartupException(
                $"Module validation failed: {error.Message} ({error.ErrorType})");
        }

        // Build dependency graph and get load order
        var graph = graphService.BuildGraph(descriptors);

        logger.LogInformation("Resolved Aero module load order: {ModuleLoadOrder}",
            string.Join(" -> ", graph.LoadOrder.Select(descriptor => descriptor.Name)));

        // Create module builder for composition
        var moduleBuilder = new ModuleBuilder(services, configuration, environment);

        // Register modules as singletons in dependency order
        foreach (var descriptor in graph.LoadOrder)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IAeroModule), descriptor.ModuleType));

            // Also register as self for concrete access
            services.TryAddSingleton(descriptor.ModuleType);

            // Register specialized interfaces
            RegisterSpecializedInterfaces(services, descriptor);
        }

        // Now that all modules are registered, build a provider to get instances
        // Note: This is still needed because modules need to be instantiated to call Configure
        // However, we do it AFTER registration in the correct order, avoiding the ordering issues
        await using var moduleProvider = services.BuildServiceProvider();

        // Execute Configure on each module in dependency order
        foreach (var descriptor in graph.LoadOrder)
        {
            var module = (IAeroModule?)moduleProvider.GetService(descriptor.ModuleType);
            if (module != null)
            {
                module.Configure(moduleBuilder);
            }
        }

        // Execute ConfigureServices on each module in dependency order
        foreach (var descriptor in graph.LoadOrder)
        {
            var module = (IAeroModule?)moduleProvider.GetService(descriptor.ModuleType);
            if (module != null)
            {
                module.ConfigureServices(services, configuration, environment);
            }
        }

        // Register the graph for later use
        services.AddSingleton(graph);

        return services;
    }

    private static void RegisterSpecializedInterfaces(IServiceCollection services, ModuleDescriptor descriptor)
    {
        if (descriptor.IsUiModule)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IUiModule), descriptor.ModuleType));
        }

        if (typeof(IApiModule).IsAssignableFrom(descriptor.ModuleType))
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IApiModule), descriptor.ModuleType));
        }

        if (typeof(IBackgroundModule).IsAssignableFrom(descriptor.ModuleType))
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IBackgroundModule), descriptor.ModuleType));
        }

        if (typeof(IThemeModule).IsAssignableFrom(descriptor.ModuleType))
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IThemeModule), descriptor.ModuleType));
        }

        if (typeof(IAdminModule).IsAssignableFrom(descriptor.ModuleType))
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IAdminModule), descriptor.ModuleType));
        }

        if (typeof(IFilterModule).IsAssignableFrom(descriptor.ModuleType))
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IFilterModule), descriptor.ModuleType));
        }

        if (typeof(IContentDefinitionModule).IsAssignableFrom(descriptor.ModuleType))
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IContentDefinitionModule), descriptor.ModuleType));
        }
    }

    /// <summary>
    /// Maps Aero module endpoints in dependency order.
    /// </summary>
    public static IEndpointRouteBuilder MapAeroModules(
        this IEndpointRouteBuilder endpoints)
    {
        var graph = endpoints.ServiceProvider.GetService<ModuleGraph>();

        if (graph != null)
        {
            // Use the graph's load order if available
            foreach (var descriptor in graph.LoadOrder)
            {
                var module = endpoints.ServiceProvider.GetService(descriptor.ModuleType) as IAeroModule;
                module?.Run(endpoints);
            }
        }
        else
        {
            // Fallback: use traditional ordering
            var modules = endpoints.ServiceProvider
                .GetServices<IAeroModule>()
                .OrderBy(m => m.Order)
                .ToList();

            foreach (var module in modules)
            {
                module.Run(endpoints);
            }
        }

        return endpoints;
    }

    public static IApplicationBuilder UseAeroCmsModules(this IApplicationBuilder app)
    {
        if (app is IEndpointRouteBuilder endpoints)
        {
            endpoints.MapAeroModules();
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
                    await module.RunAsync(endpoints);
                }
            }
        }
        else
        {
            // Fallback: use traditional ordering
            var modules = endpoints.ServiceProvider
                .GetServices<IAeroModule>()
                .OrderBy(m => m.Order)
                .ToList();

            foreach (var module in modules)
            {
                await module.RunAsync(endpoints);
            }
        }

        return endpoints;
    }

    // Helper methods to get specific module types
    public static IEnumerable<T> GetModules<T>(this IServiceProvider provider)
        where T : IAeroModule
    {
        return provider.GetServices<T>().OrderBy(m => m.Order);
    }

    public static IEnumerable<IUiModule> GetUiModules(this IServiceProvider provider)
        => provider.GetModules<IUiModule>();

    public static IEnumerable<IApiModule> GetApiModules(this IServiceProvider provider)
        => provider.GetModules<IApiModule>();

    public static IEnumerable<IBackgroundModule> GetBackgroundModules(this IServiceProvider provider)
        => provider.GetModules<IBackgroundModule>();

    public static IEnumerable<IThemeModule> GetThemeModules(this IServiceProvider provider)
        => provider.GetModules<IThemeModule>();

    public static IEnumerable<IAdminModule> GetAdminModules(this IServiceProvider provider)
        => provider.GetModules<IAdminModule>();

    public static IEnumerable<IFilterModule> GetFilterModules(this IServiceProvider provider)
        => provider.GetModules<IFilterModule>();

    public static IEnumerable<IContentDefinitionModule> GetContentDefinitionModules(this IServiceProvider provider)
        => provider.GetModules<IContentDefinitionModule>();
}

/// <summary>
/// Exception thrown when the module system fails during startup.
/// </summary>
public class ModuleSystemStartupException : Exception
{
    public ModuleSystemStartupException(string message) : base(message) { }
    public ModuleSystemStartupException(string message, Exception inner) : base(message, inner) { }
}
