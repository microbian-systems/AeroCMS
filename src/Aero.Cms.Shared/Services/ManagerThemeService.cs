using Microsoft.JSInterop;

namespace Aero.Cms.Shared.Services;

/// <summary>
/// Represents a class for ManagerThemeService.
/// </summary>
public class ManagerThemeService(IJSRuntime jsRuntime)
{
    private bool _isDarkMode = true;
    private bool _isInitialized;
        /// <summary>
    /// Gets or sets the Is Dark Mode.
    /// </summary>
public bool IsDarkMode => _isDarkMode;
        /// <summary>
    /// Gets or sets the Theme.
    /// </summary>
public string Theme => _isDarkMode ? "dark" : "light";
        /// <summary>
    /// Gets or sets the Is Sidebar Collapsed.
    /// </summary>
public bool IsSidebarCollapsed { get; private set; }

        /// <summary>
    /// Event raised for OnThemeChanged.
    /// </summary>
public event Action? OnThemeChanged;

        /// <summary>
    /// InitializeAsync method.
    /// </summary>
public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;
        var savedTheme = await GetPersistedThemeAsync();
        if (TryNormalizeTheme(savedTheme, out var theme))
        {
            _isDarkMode = theme == "dark";
        }

        // Apply data-theme on <body> so Radzen portal elements (outside .pe-root) inherit CSS variables
        await SyncDomThemeAsync();
        await SyncRadzenThemeAsync();
        NotifyChanged();
    }

        /// <summary>
    /// SetDarkModeAsync method.
    /// </summary>
public async Task SetDarkModeAsync(bool isDark)
    {
        _isDarkMode = isDark;
        await PersistThemeAsync();
        await SyncDomThemeAsync();
        await SyncRadzenThemeAsync();
        NotifyChanged();
    }

        /// <summary>
    /// ToggleThemeAsync method.
    /// </summary>
public async Task ToggleThemeAsync()
    {
        _isDarkMode = !_isDarkMode;
        await PersistThemeAsync();
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
                $"document.documentElement.setAttribute('data-theme', '{Theme}'); document.body.setAttribute('data-theme', '{Theme}')");
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

    private async Task<string?> GetPersistedThemeAsync()
    {
        try
        {
            return await jsRuntime.InvokeAsync<string?>("eval", @"
                (function () {
                    const names = ['manager-theme', 'AeroCms.ManagerTheme', 'AeroCms.Theme', 'aero-manager-theme'];
                    const cookies = document.cookie ? document.cookie.split(';') : [];
                    for (const name of names) {
                        const prefix = name + '=';
                        const match = cookies.map(c => c.trim()).find(c => c.startsWith(prefix));
                        if (match) return decodeURIComponent(match.substring(prefix.length));
                    }

                    return localStorage.getItem('manager-theme');
                })()
            ");
        }
        catch
        {
            return null;
        }
    }

    private async Task PersistThemeAsync()
    {
        try
        {
            await jsRuntime.InvokeVoidAsync("eval", $@"
                localStorage.setItem('manager-theme', '{Theme}');
                document.cookie = 'manager-theme={Theme}; path=/; max-age=31536000; SameSite=Lax';
            ");
        }
        catch
        {
            // JS interop may not be available during prerendering
        }
    }

    private static bool TryNormalizeTheme(string? value, out string theme)
    {
        theme = string.Equals(value, "light", StringComparison.OrdinalIgnoreCase) ? "light" : "dark";
        return string.Equals(value, "light", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "dark", StringComparison.OrdinalIgnoreCase);
    }

        /// <summary>
    /// ToggleSidebar method.
    /// </summary>
public void ToggleSidebar()
    {
        IsSidebarCollapsed = !IsSidebarCollapsed;
        NotifyChanged();
    }

    private void NotifyChanged() => OnThemeChanged?.Invoke();
}
