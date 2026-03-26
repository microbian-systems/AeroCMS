using Microsoft.JSInterop;

namespace Aero.Cms.Shared.Services;

public class ManagerThemeService(IJSRuntime jsRuntime)
{
    private bool _isDarkMode = true;
    public bool IsDarkMode => _isDarkMode;
    public string Theme => _isDarkMode ? "dark" : "light";
    public bool IsSidebarCollapsed { get; private set; }

    public event Action? OnThemeChanged;

    public async Task InitializeAsync()
    {
        var savedTheme = await jsRuntime.InvokeAsync<string?>("localStorage.getItem", "manager-theme");
        if (savedTheme != null)
        {
            _isDarkMode = savedTheme == "dark";
            NotifyChanged();
        }
    }

    public async Task SetDarkModeAsync(bool isDark)
    {
        _isDarkMode = isDark;
        await jsRuntime.InvokeVoidAsync("localStorage.setItem", "manager-theme", _isDarkMode ? "dark" : "light");
        NotifyChanged();
    }

    public async Task ToggleThemeAsync()
    {
        _isDarkMode = !_isDarkMode;
        await jsRuntime.InvokeVoidAsync("localStorage.setItem", "manager-theme", _isDarkMode ? "dark" : "light");
        NotifyChanged();
    }

    public void ToggleSidebar()
    {
        IsSidebarCollapsed = !IsSidebarCollapsed;
        NotifyChanged();
    }

    private void NotifyChanged() => OnThemeChanged?.Invoke();
}
