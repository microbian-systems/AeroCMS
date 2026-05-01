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
    /// Registers core module system services (discovery, graph, state merger, etc.)
    /// </summary>
    public static IServiceCollection AddModuleSystemServices(this IServiceCollection services)
    {
        // Register discovery service
        services.TryAddScoped<IModuleDiscoveryService, ModuleDiscoveryService>();

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
        IReadOnlyList<ModuleDescriptor>? generatedDescriptors = null,
        ModuleCatalogMode catalogMode = ModuleCatalogMode.LegacyFallbackAllowed)
        => services.AddAeroModulesAsync(config, env, generatedDescriptors, catalogMode).GetAwaiter().GetResult();

    /// <summary>
    /// Alias for <see cref="AddAeroModules"/>.
    /// </summary>
    public static IServiceCollection AddAeroCmsModules(
        this IServiceCollection services,
        IConfiguration config,
        IHostEnvironment env,
        IReadOnlyList<ModuleDescriptor>? generatedDescriptors = null,
        ModuleCatalogMode catalogMode = ModuleCatalogMode.LegacyFallbackAllowed)
        => services.AddAeroModules(config, env, generatedDescriptors, catalogMode);

    /// <summary>
    /// Discovers, validates, and registers Aero modules in dependency order.
    /// Supports both source-generated catalogs and legacy reflection fallback.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="environment">Host environment.</param>
    /// <param name="generatedDescriptors">
    /// Source-generated module descriptors. When non-null, skips reflection-based
    /// discovery and uses these descriptors directly. When null, falls back to
    /// the legacy <see cref="IModuleDiscoveryService"/> reflection scanning.
    /// </param>
    /// <param name="catalogMode">
    /// Controls behavior when generated descriptors are null or empty.
    /// Defaults to <see cref="ModuleCatalogMode.LegacyFallbackAllowed"/>.
    /// </param>
    /// <exception cref="ModuleSystemStartupException">
    /// Thrown when <paramref name="catalogMode"/> is <see cref="ModuleCatalogMode.GeneratedRequired"/>
    /// and <paramref name="generatedDescriptors"/> is null or empty.
    /// </exception>
    public static async Task<IServiceCollection> AddAeroModulesAsync(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        IReadOnlyList<ModuleDescriptor>? generatedDescriptors = null,
        ModuleCatalogMode catalogMode = ModuleCatalogMode.LegacyFallbackAllowed)
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

        // Construct the merger manually: the temp provider doesn't register IModuleStateStore,
        // so GetService (not GetRequiredService) is used to allow optional null.
        var sp = scope.ServiceProvider;
        var discoveryService = sp.GetRequiredService<IModuleDiscoveryService>();
        var graphService = sp.GetRequiredService<IModuleGraphService>();
        var stateMerger = new ModuleRuntimeStateMerger(
            sp.GetService<IModuleStateStore>(),
            sp.GetRequiredService<ILogger<ModuleRuntimeStateMerger>>());
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Aero.Cms.Modules.Startup");

        // Resolve descriptors: use generated catalog or fall back to reflection
        IReadOnlyList<ModuleDescriptor> descriptors;

        if (generatedDescriptors is not null)
        {
            // Source-generated path
            if (generatedDescriptors.Count == 0 && catalogMode == ModuleCatalogMode.GeneratedRequired)
            {
                throw new ModuleSystemStartupException(
                    "Generated module catalog is empty but ModuleCatalogMode.GeneratedRequired " +
                    "was specified. The source generator or analyzer reference may be misconfigured.");
            }

            logger.LogInformation("Using source-generated module catalog with {Count} descriptor(s).",
                generatedDescriptors.Count);

            // Merge with stored state
            descriptors = await stateMerger.MergeAsync(generatedDescriptors);
        }
        else
        {
            // Legacy reflection fallback
            if (catalogMode == ModuleCatalogMode.GeneratedRequired)
            {
                throw new ModuleSystemStartupException(
                    "Generated module catalog is null but ModuleCatalogMode.GeneratedRequired " +
                    "was specified. The source generator may not have executed.");
            }

            logger.LogInformation("No generated catalog provided — falling back to reflection-based discovery.");

            descriptors = await discoveryService.DiscoverAsync();

            // Merge with stored state after reflection discovery
            descriptors = await stateMerger.MergeAsync(descriptors);
        }

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
