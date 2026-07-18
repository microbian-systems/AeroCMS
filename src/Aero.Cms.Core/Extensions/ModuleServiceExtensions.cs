using Aero.Modular;

namespace Aero.Cms.Core.Extensions;

/// <summary>
/// Service-provider helpers for the source-generated Aero module catalog.
/// </summary>
public static class ModuleServiceExtensions
{
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
