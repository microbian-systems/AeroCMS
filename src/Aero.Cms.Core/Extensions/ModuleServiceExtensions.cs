using Aero.Modular;

namespace Aero.Cms.Core.Extensions;

/// <summary>
/// Service-provider helpers for the source-generated Aero module catalog.
/// </summary>
public static class ModuleServiceExtensions
{
    /// <summary>Resolves registered modules of a type, ordered by their declared order.</summary>
    /// <typeparam name="T">The module contract to resolve.</typeparam><param name="provider">The service provider to query.</param>
    /// <returns>The registered modules ordered by <see cref="IAeroModule.Order"/>.</returns>
    /// <remarks>
    /// All registrations are preserved, including equal-order and duplicate registrations.
    /// Module activation failures from the service provider propagate to the caller.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
    public static IEnumerable<T> GetModules<T>(this IServiceProvider provider)
        where T : IAeroModule
    {
        return provider.GetServices<T>().OrderBy(m => m.Order);
    }

    /// <summary>Resolves registered UI modules in declared order.</summary><param name="provider">The service provider to query.</param>
    /// <returns>The registered UI modules ordered by module order.</returns>
    public static IEnumerable<IUiModule> GetUiModules(this IServiceProvider provider)
        => provider.GetModules<IUiModule>();

    /// <summary>Resolves registered API modules in declared order.</summary><param name="provider">The service provider to query.</param>
    /// <returns>The registered API modules ordered by module order.</returns>
    public static IEnumerable<IApiModule> GetApiModules(this IServiceProvider provider)
        => provider.GetModules<IApiModule>();

    /// <summary>Resolves registered background modules in declared order.</summary><param name="provider">The service provider to query.</param>
    /// <returns>The registered background modules ordered by module order.</returns>
    public static IEnumerable<IBackgroundModule> GetBackgroundModules(this IServiceProvider provider)
        => provider.GetModules<IBackgroundModule>();

    /// <summary>Resolves registered theme modules in declared order.</summary><param name="provider">The service provider to query.</param>
    /// <returns>The registered theme modules ordered by module order.</returns>
    public static IEnumerable<IThemeModule> GetThemeModules(this IServiceProvider provider)
        => provider.GetModules<IThemeModule>();

    /// <summary>Resolves registered administration modules in declared order.</summary><param name="provider">The service provider to query.</param>
    /// <returns>The registered administration modules ordered by module order.</returns>
    public static IEnumerable<IAdminModule> GetAdminModules(this IServiceProvider provider)
        => provider.GetModules<IAdminModule>();

    /// <summary>Resolves registered filter modules in declared order.</summary><param name="provider">The service provider to query.</param>
    /// <returns>The registered filter modules ordered by module order.</returns>
    public static IEnumerable<IFilterModule> GetFilterModules(this IServiceProvider provider)
        => provider.GetModules<IFilterModule>();

    /// <summary>Resolves registered content-definition modules in declared order.</summary><param name="provider">The service provider to query.</param>
    /// <returns>The registered content-definition modules ordered by module order.</returns>
    public static IEnumerable<IContentDefinitionModule> GetContentDefinitionModules(this IServiceProvider provider)
        => provider.GetModules<IContentDefinitionModule>();
}
