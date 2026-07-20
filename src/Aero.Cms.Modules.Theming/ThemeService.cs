using Aero.Cms.Core.Extensions;

namespace Aero.Cms.Modules.Theming;


/// <summary>
/// Resolves the theme name selected for rendering.
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// Gets the active theme name.
    /// </summary>
    /// <returns>The discovered theme name, or <c>Default</c> when no theme is registered.</returns>
Task<string> GetActiveThemeAsync();
}

/// <summary>
/// Resolves the first registered theme module as a placeholder active-theme implementation.
/// </summary>
/// <remarks>
/// The current class does not initialize its <see cref="IServiceProvider"/> field through a
/// constructor or assignment. Calling <see cref="GetActiveThemeAsync"/> therefore dereferences
/// an uninitialized provider in the current implementation.
/// </remarks>
public class ThemeService : IThemeService
{
    /// <summary>
    /// Holds the provider used for theme-module discovery.
    /// </summary>
    private readonly IServiceProvider sp;

    /// <summary>
    /// Returns the first discovered theme name, without consulting persisted activation state.
    /// </summary>
    /// <returns>The first theme name, or <c>Default</c> when discovery returns no modules.</returns>
public async Task<string> GetActiveThemeAsync()
    {
        var themeModules = sp.GetThemeModules();
        var activeTheme = themeModules.FirstOrDefault(t => true /* check active */);
        return activeTheme?.Name ?? "Default";
    }
}