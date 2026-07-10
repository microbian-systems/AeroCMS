using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Core.Blocks;
using Aero.Cms.Core.Blocks.Dynamic;
using Aero.Core.Security;
using Aero.Modular;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aero.Cms.Core.Extensions;

public static class BlockServiceExtensions
{
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
