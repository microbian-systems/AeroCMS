using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Core.Blocks;
using Aero.Cms.Core.Blocks.Dynamic;
using Aero.Core.Security;
using Aero.Modular;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aero.Cms.Core.Extensions;

/// <summary>
/// Represents a class for BlockServiceExtensions.
/// </summary>
public static class BlockServiceExtensions
{
        /// <summary>
    /// AddBlockSystemServices method.
    /// </summary>
public static IServiceCollection AddBlockSystemServices(this IServiceCollection services)
    {
        services.TryAddScoped<IBlockService, AeroBlockService>();
        // Per-request block cache that eliminates N+1 DB round-trips during
        // page rendering. DynamicPageModel preloads all block IDs in one
        // batch query; BlockPlacementRenderer reads from this cache instead
        // of calling IBlockService.GetByIdAsync individually.
        services.TryAddScoped<BlockRenderCache>();
        services.TryAddSingleton<IHtmlSanitizer, HtmlSanitizer>();
        services.TryAddSingleton<SecureScribanTemplateOptions>();
        services.TryAddSingleton<DynamicTemplateValidator>();
        services.TryAddSingleton<ISecureScribanRenderer, SecureScribanRenderer>();
        services.TryAddScoped<IDynamicBlockDefinitionService, AeroDynamicBlockDefinitionService>();
        services.AddSingleton<global::AeroDB.Sable.IConfigureAeroDB, BlockAeroDbConfiguration>();
        return services;
    }

    // Helper methods to get specific module types from DI

        /// <summary>
    /// GetModules method.
    /// </summary>
public static IEnumerable<T> GetModules<T>(this IServiceProvider provider)
        where T : IAeroModule
    {
        return provider.GetServices<T>().OrderBy(m => m.Order);
    }

        /// <summary>
    /// GetUiModules method.
    /// </summary>
public static IEnumerable<IUiModule> GetUiModules(this IServiceProvider provider)
        => provider.GetModules<IUiModule>();

        /// <summary>
    /// GetApiModules method.
    /// </summary>
public static IEnumerable<IApiModule> GetApiModules(this IServiceProvider provider)
        => provider.GetModules<IApiModule>();

        /// <summary>
    /// GetBackgroundModules method.
    /// </summary>
public static IEnumerable<IBackgroundModule> GetBackgroundModules(this IServiceProvider provider)
        => provider.GetModules<IBackgroundModule>();

        /// <summary>
    /// GetThemeModules method.
    /// </summary>
public static IEnumerable<IThemeModule> GetThemeModules(this IServiceProvider provider)
        => provider.GetModules<IThemeModule>();

        /// <summary>
    /// GetAdminModules method.
    /// </summary>
public static IEnumerable<IAdminModule> GetAdminModules(this IServiceProvider provider)
        => provider.GetModules<IAdminModule>();

        /// <summary>
    /// GetFilterModules method.
    /// </summary>
public static IEnumerable<IFilterModule> GetFilterModules(this IServiceProvider provider)
        => provider.GetModules<IFilterModule>();

        /// <summary>
    /// GetContentDefinitionModules method.
    /// </summary>
public static IEnumerable<IContentDefinitionModule> GetContentDefinitionModules(this IServiceProvider provider)
        => provider.GetModules<IContentDefinitionModule>();
}
