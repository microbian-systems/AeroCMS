using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Aero.Core.Extensions;

namespace Aero.Cms.Core.Modules;

// todo - abstract/extract Aero modules into its own lib so it can be used in any type of app (host, console, web, etc)

public static class ModuleExtensions
{
    public static async Task<WebApplicationBuilder> AddAeroCms<T>(this WebApplicationBuilder builder)
        where T : class => await builder.AddAeroCms<T>([]);

    public static async Task<WebApplicationBuilder> AddAeroCms<T>(this WebApplicationBuilder builder, string[] args)
        where T : class
    {
        var config = builder.Configuration;
        var services = builder.Services;
        var env = builder.Environment;

        _ = config.AddConfiguration<T>(env);
        var log = await services.ConfigureLogging(config);

        return builder;
    }

    public static IServiceCollection AddAeroModules(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // Register all IAeroModule implementations
        services.Scan(scan => scan
            .FromApplicationDependencies()
            .AddClasses(classes => classes.AssignableTo<IAeroModule>())
            .AsImplementedInterfaces()
            .AsSelf()
            .WithSingletonLifetime());

        // Create the module builder
        var moduleBuilder = new ModuleBuilder(services, configuration, environment);

        // Build a temporary service provider to resolve modules
        using var tempProvider = services.BuildServiceProvider();

        // Get all registered module instances ordered by Order property
        var modules = tempProvider.GetServices<IAeroModule>()
            .OrderBy(m => m.Order)
            .ToList();

        // Execute Configure on each module (composition surface)
        foreach (var module in modules)
        {
            module.Configure(moduleBuilder);
        }

        // Execute ConfigureServices on each module (service registration)
        foreach (var module in modules)
        {
            module.ConfigureServices(services, configuration, environment);
        }

        // Register the module builder itself for access to permissions/content types
        // services.AddSingleton<IReadOnlySet<string>>(sp => moduleBuilder.Permissions);
        // services.AddSingleton(moduleBuilder.Permissions);
        // services.AddSingleton(moduleBuilder.ContentTypes);

        return services;
    }

    public static IEndpointRouteBuilder MapAeroModules(
        this IEndpointRouteBuilder endpoints)
    {
        var modules = endpoints.ServiceProvider
            .GetServices<IAeroModule>()
            .OrderBy(m => m.Order)
            .ToList();

        foreach (var module in modules)
        {
            module.Run(endpoints);
        }

        return endpoints;
    }

    public static async Task<IEndpointRouteBuilder> MapAeroAppAsync(
        this IEndpointRouteBuilder endpoints)
    {
        var modules = endpoints.ServiceProvider
            .GetServices<IAeroModule>()
            .OrderBy(m => m.Order)
            .ToList();

        // Execute asynchronous RunAsync
        foreach (var module in modules)
        {
            await module.RunAsync(endpoints);
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