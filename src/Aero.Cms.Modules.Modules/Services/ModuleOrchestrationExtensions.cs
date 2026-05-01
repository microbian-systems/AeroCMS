using Aero.Modular;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Modules.Services;

/// <summary>
/// Extension methods for module registration and initialization in ASP.NET Core applications.
/// </summary>
public static class ModuleOrchestrationExtensions
{
    /// <summary>
    /// Registers core module system services (discovery, graph, etc.)
    /// </summary>
    public static IServiceCollection AddModuleSystemServices(this IServiceCollection services)
    {
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

    /// <summary>
    /// Alias for <see cref="AddAeroModules"/>.
    /// </summary>
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
        var moduleBuilder = new AeroModuleBuilder(services, configuration, environment);

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

        // Automatically register Marten configuration if implemented by the module
        if (typeof(global::Marten.IConfigureMarten).IsAssignableFrom(descriptor.ModuleType))
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(global::Marten.IConfigureMarten), descriptor.ModuleType));
        }

        if (typeof(global::Marten.IAsyncConfigureMarten).IsAssignableFrom(descriptor.ModuleType))
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(global::Marten.IAsyncConfigureMarten), descriptor.ModuleType));
        }
    }

}

