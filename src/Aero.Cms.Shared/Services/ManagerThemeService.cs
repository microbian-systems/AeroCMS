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
        }
        // Apply data-theme on <body> so Radzen portal elements (outside .pe-root) inherit CSS variables
        await SyncDomThemeAsync();
        await SyncRadzenThemeAsync();
        NotifyChanged();
    }

    public async Task SetDarkModeAsync(bool isDark)
    {
        _isDarkMode = isDark;
        await jsRuntime.InvokeVoidAsync("localStorage.setItem", "manager-theme", _isDarkMode ? "dark" : "light");
        await SyncDomThemeAsync();
        await SyncRadzenThemeAsync();
        NotifyChanged();
    }

    public async Task ToggleThemeAsync()
    {
        _isDarkMode = !_isDarkMode;
        await jsRuntime.InvokeVoidAsync("localStorage.setItem", "manager-theme", _isDarkMode ? "dark" : "light");
        await SyncDomThemeAsync();
        await SyncRadzenThemeAsync();
        NotifyChanged();
    }

    private async Task SyncDomThemeAsync()
    {
        try
        {
            // Propagate data-theme to <body> so Radzen portal elements (popups, dropdowns, dialogs)
            // that render outside .pe-root still inherit --pe-* and --rz-* CSS variables
            await jsRuntime.InvokeVoidAsync("eval",
                $"document.body.setAttribute('data-theme', '{(_isDarkMode ? "dark" : "light")}')");
        }
        catch
        {
            // JS interop may not be available during prerendering
        }
    }

    private async Task SyncRadzenThemeAsync()
    {
        try
        {
            // Radzen Standard theme uses file-swap, not .rz-dark class
            // Toggle the dark CSS link's disabled state
            await jsRuntime.InvokeVoidAsync("eval", @"
                var link = document.getElementById('radzen-theme-link');
                if (link) link.disabled = " + (_isDarkMode ? "false" : "true") + @";
            ");
        }
        catch
        {
            // JS interop may not be available during prerendering
        }
    }

    public void ToggleSidebar()
    {
        IsSidebarCollapsed = !IsSidebarCollapsed;
        NotifyChanged();
    }

    private void NotifyChanged() => OnThemeChanged?.Invoke();
}
