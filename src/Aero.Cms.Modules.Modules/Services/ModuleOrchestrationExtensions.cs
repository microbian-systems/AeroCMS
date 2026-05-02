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
    /// Registers core module system services (graph, state merger, options).
    /// Reflection-based discovery is no longer required — use generated descriptors.
    /// </summary>
    public static IServiceCollection AddModuleSystemServices(this IServiceCollection services)
    {
        // Register graph service
        services.TryAddScoped<IModuleGraphService, ModuleGraphService>();

        // Register runtime state merger
        services.TryAddScoped<IModuleRuntimeStateMerger, ModuleRuntimeStateMerger>();

        // Register options
        services.AddOptions<ModuleDiscoveryOptions>();
        services.AddOptions<ModuleGraphOptions>();

        return services;
    }

    /// <summary>
    /// Synchronous wrapper for adding Aero modules with generated catalog.
    /// </summary>
    public static IServiceCollection AddAeroModules(
        this IServiceCollection services,
        IConfiguration config,
        IHostEnvironment env,
        IReadOnlyList<ModuleDescriptor> generatedDescriptors)
        => services.AddAeroModulesAsync(config, env, generatedDescriptors).GetAwaiter().GetResult();

    /// <summary>
    /// Alias for <see cref="AddAeroModules"/>.
    /// </summary>
    public static IServiceCollection AddAeroCmsModules(
        this IServiceCollection services,
        IConfiguration config,
        IHostEnvironment env,
        IReadOnlyList<ModuleDescriptor> generatedDescriptors)
        => services.AddAeroModules(config, env, generatedDescriptors);

    /// <summary>
    /// Validates and registers source-generated Aero modules in dependency order.
    /// Requires a non-null, non-empty generated descriptor catalog.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="environment">Host environment.</param>
    /// <param name="generatedDescriptors">
    /// Source-generated module descriptors. Must be non-null and non-empty.
    /// </param>
    /// <exception cref="ModuleSystemStartupException">
    /// Thrown when <paramref name="generatedDescriptors"/> is null or empty.
    /// </exception>
    public static async Task<IServiceCollection> AddAeroModulesAsync(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        IReadOnlyList<ModuleDescriptor> generatedDescriptors)
    {
        ArgumentNullException.ThrowIfNull(generatedDescriptors);

        if (generatedDescriptors.Count == 0)
        {
            throw new ModuleSystemStartupException(
                "Generated module catalog is empty. The source generator or analyzer reference may be misconfigured.");
        }

        // Use local service provider for setup phase (logging, graph service)
        var setupServices = new ServiceCollection();
        setupServices.AddSingleton(environment);
        setupServices.AddLogging();
        setupServices.AddOptions();
        setupServices.AddModuleSystemServices();
        setupServices.Configure<ModuleDiscoveryOptions>(configuration.GetSection("ModuleDiscovery"));

        await using var setupProvider = setupServices.BuildServiceProvider();
        using var scope = setupProvider.CreateScope();
        var sp = scope.ServiceProvider;

        var graphService = sp.GetRequiredService<IModuleGraphService>();
        var stateMerger = new ModuleRuntimeStateMerger(
            sp.GetService<IModuleStateStore>(),
            sp.GetRequiredService<ILogger<ModuleRuntimeStateMerger>>());
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Aero.Cms.Modules.Startup");

        // Use source-generated descriptors merged with stored database state
        logger.LogInformation("Using source-generated module catalog with {Count} descriptor(s).",
            generatedDescriptors.Count);

        var descriptors = await stateMerger.MergeAsync(generatedDescriptors);

        logger.LogInformation("Discovered {ModuleCount} Aero modules: {ModuleNames}",
            descriptors.Count,
            string.Join(", ", descriptors.Select(d => d.Name).OrderBy(name => name)));

        if (descriptors.Count == 0)
        {
            // No modules discovered — register empty module set
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
            string.Join(" -> ", graph.LoadOrder.Select(d => d.Name)));

        // Create module builder for composition
        var moduleBuilder = new AeroModuleBuilder(services, configuration, environment);

        // Register modules as singletons in dependency order
        foreach (var descriptor in graph.LoadOrder)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IAeroModule), descriptor.ModuleType));

            // Also register as self for concrete access
            services.TryAddSingleton(descriptor.ModuleType);

            // Register specialized interfaces (reflection-free for generated descriptors)
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
        // Use marker flags from ModuleDescriptor first (source-generated / metadata-driven)
        // Fall back to IsAssignableFrom for legacy reflection descriptors where flags may be unset

        if (descriptor.IsUiModule || typeof(IUiModule).IsAssignableFrom(descriptor.ModuleType))
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IUiModule), descriptor.ModuleType));
        }

        if (descriptor.IsApiModule || typeof(IApiModule).IsAssignableFrom(descriptor.ModuleType))
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IApiModule), descriptor.ModuleType));
        }

        if (descriptor.IsBackgroundModule || typeof(IBackgroundModule).IsAssignableFrom(descriptor.ModuleType))
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IBackgroundModule), descriptor.ModuleType));
        }

        if (descriptor.IsThemeModule || typeof(IThemeModule).IsAssignableFrom(descriptor.ModuleType))
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IThemeModule), descriptor.ModuleType));
        }

        if (descriptor.IsAdminModule || typeof(IAdminModule).IsAssignableFrom(descriptor.ModuleType))
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IAdminModule), descriptor.ModuleType));
        }

        if (descriptor.IsFilterModule || typeof(IFilterModule).IsAssignableFrom(descriptor.ModuleType))
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IFilterModule), descriptor.ModuleType));
        }

        if (descriptor.IsContentDefinitionModule || typeof(IContentDefinitionModule).IsAssignableFrom(descriptor.ModuleType))
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IContentDefinitionModule), descriptor.ModuleType));
        }

        // Automatically register Marten configuration if implemented by the module
        if (descriptor.IsMartenConfigurator || typeof(global::Marten.IConfigureMarten).IsAssignableFrom(descriptor.ModuleType))
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(global::Marten.IConfigureMarten), descriptor.ModuleType));
        }

        if (descriptor.IsAsyncMartenConfigurator || typeof(global::Marten.IAsyncConfigureMarten).IsAssignableFrom(descriptor.ModuleType))
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(global::Marten.IAsyncConfigureMarten), descriptor.ModuleType));
        }
    }
}

/// <summary>
/// Controls module catalog sourcing and failure behavior.
/// </summary>
public enum ModuleCatalogMode
{
    /// <summary>
    /// When generated descriptors are null, fall back to legacy reflection discovery.
    /// When generated descriptors are empty, return no modules without error.
    /// Suitable for tests, tools, and transitional setups.
    /// </summary>
    LegacyFallbackAllowed,

    /// <summary>
    /// Require a non-null, non-empty generated descriptor catalog.
    /// Throws <see cref="ModuleSystemStartupException"/> if the catalog is
    /// null or empty. Used by <c>Aero.Cms.Web</c> to fail loudly when the
    /// source generator or analyzer reference is broken.
    /// </summary>
    GeneratedRequired,
}
