using Aero.Cms.Web.Core.Modules;

namespace Aero.Cms.Modules.Theming;


public interface IThemeService
{
    Task<string> GetActiveThemeAsync();
}

public class ThemeService : IThemeService
{
    private readonly IServiceProvider sp;

    public async Task<string> GetActiveThemeAsync()
    {
        var themeModules = sp.GetThemeModules();
        var activeTheme = themeModules.FirstOrDefault(t => true /* check active */);
        return activeTheme?.Name ?? "Default";
    }
}