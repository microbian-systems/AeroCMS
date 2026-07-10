using Aero.Cms.Core.Extensions;

namespace Aero.Cms.Modules.Theming;


/// <summary>
/// Defines an interface for IThemeService.
/// </summary>
public interface IThemeService
{
        /// <summary>
    /// GetActiveThemeAsync method.
    /// </summary>
Task<string> GetActiveThemeAsync();
}

/// <summary>
/// Represents a class for ThemeService.
/// </summary>
public class ThemeService : IThemeService
{
    private readonly IServiceProvider sp;

        /// <summary>
    /// GetActiveThemeAsync method.
    /// </summary>
public async Task<string> GetActiveThemeAsync()
    {
        var themeModules = sp.GetThemeModules();
        var activeTheme = themeModules.FirstOrDefault(t => true /* check active */);
        return activeTheme?.Name ?? "Default";
    }
}