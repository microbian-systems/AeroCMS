using System.Globalization;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Abstractions.Requests;
using Aero.Cms.Abstractions.Theming;
using Aero.Core;
using Aero.Core.Globalization;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;

namespace Aero.Cms.Shared.Pages.Manager.Sites;

/// <summary>
/// Represents a class for SiteEditor.
/// </summary>
public partial class SiteEditor : ComponentBase
{
        /// <summary>
    /// Gets or sets the Id.
    /// </summary>
[Parameter] public long? Id { get; set; }

        /// <summary>
    /// Gets or sets the Sites Client.
    /// </summary>
[Inject] protected ISitesHttpClient SitesClient { get; set; } = null!;
        /// <summary>
    /// Gets or sets the Themes Client.
    /// </summary>
[Inject] protected IThemesHttpClient ThemesClient { get; set; } = null!;
        /// <summary>
    /// Gets or sets the Navigation.
    /// </summary>
[Inject] protected NavigationManager Navigation { get; set; } = null!;
        /// <summary>
    /// Gets or sets the L.
    /// </summary>
[Inject] protected IStringLocalizer<Aero.Cms.Shared.Localization.ManagerResource> L { get; set; } = default!;

        /// <summary>
    /// Gets or sets the Is Loading.
    /// </summary>
protected bool IsLoading { get; set; }
        /// <summary>
    /// Gets or sets the Is Saving.
    /// </summary>
protected bool IsSaving { get; set; }
        /// <summary>
    /// Gets or sets whether a theme selection is being applied.
    /// </summary>
protected bool IsApplyingTheme { get; set; }
        /// <summary>
    /// Gets or sets whether the deployment theme catalog is being loaded.
    /// </summary>
protected bool IsThemeCatalogLoading { get; set; }
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
protected string Name { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Primary Host.
    /// </summary>
protected string PrimaryHost { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
protected string Description { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Host Draft.
    /// </summary>
protected string HostDraft { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Locale Search.
    /// </summary>
protected string LocaleSearch { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Default Culture.
    /// </summary>
protected string DefaultCulture { get; set; } = "en-US";
        /// <summary>
    /// Gets or sets the Hosts.
    /// </summary>
protected List<string> Hosts { get; } = [];
        /// <summary>
    /// Gets or sets the Supported Cultures.
    /// </summary>
protected List<string> SupportedCultures { get; } = ["en-US"];
        /// <summary>
    /// Gets or sets the Locale Options.
    /// </summary>
protected IReadOnlyList<AeroLocaleOption> LocaleOptions { get; } = AeroLocaleCatalog.GetLocales();
protected decimal SmallScreenBreakpointRem { get; set; } = 48;
protected List<StyleTokenEditorModel> StyleTokens { get; } = CreateDefaultStyleTokens();
protected IReadOnlyList<ThemeSummary> ThemeCatalog { get; private set; } = [];
protected string CurrentThemeId { get; private set; } = BuiltInThemeDefaults.Id;
protected string CurrentThemeVersion { get; private set; } = BuiltInThemeDefaults.Version;
protected long CurrentThemeRevision { get; private set; } = 1;
protected int SelectedThemeIndex { get; set; } = -1;
protected string? ThemeCatalogError { get; private set; }
protected string? ThemeApplyError { get; private set; }

    private long _styleProfileRevision = 1;
    private List<SiteStyleColorTokenViewModel> _additionalStyleTokens = [];

        /// <summary>
    /// Gets or sets the Is New.
    /// </summary>
protected bool IsNew => Id is null or 0;
        /// <summary>
    /// Gets or sets the Page Title.
    /// </summary>
protected string PageTitle => IsNew ? L["New Site"] : $"{L["Edit"]} {Name}";
        /// <summary>
    /// Gets or sets the Save Button Text.
    /// </summary>
protected string SaveButtonText => IsSaving ? L["Saving..."] : IsNew ? L["Create Site"] : L["Save Site"];
protected bool IsCurrentThemeInCatalog => FindThemeIndex(CurrentThemeId, CurrentThemeVersion) >= 0;
protected bool CanApplyTheme
    => !IsNew
       && !IsLoading
       && !IsThemeCatalogLoading
       && !IsApplyingTheme
       && !IsSaving
       && SelectedThemeIndex >= 0
       && SelectedThemeIndex < ThemeCatalog.Count
       && !IsSelectedThemeCurrent;

private bool IsSelectedThemeCurrent
    => SelectedThemeIndex >= 0
       && SelectedThemeIndex < ThemeCatalog.Count
       && string.Equals(ThemeCatalog[SelectedThemeIndex].Id, CurrentThemeId, StringComparison.Ordinal)
       && string.Equals(ThemeCatalog[SelectedThemeIndex].Version, CurrentThemeVersion, StringComparison.Ordinal);

private void OpenThemeStudio()
{
    if (Id is long siteId && siteId > 0)
        Navigation.NavigateTo($"/manager/sites/{siteId}/theme-studio");
}

        /// <summary>
    /// Gets or sets the Filtered Locales.
    /// </summary>
protected IEnumerable<AeroLocaleOption> FilteredLocales
    {
        get
        {
            var query = LocaleSearch.Trim();
            var options = LocaleOptions;
            if (string.IsNullOrWhiteSpace(query))
                return options;

            return options.Where(option =>
                option.CultureName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || option.EnglishName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || option.NativeName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || option.RegionName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || option.RegionCode.Contains(query, StringComparison.OrdinalIgnoreCase)
                || option.LanguageName.Contains(query, StringComparison.OrdinalIgnoreCase));
        }
    }

        /// <summary>
    /// OnParametersSetAsync method.
    /// </summary>
protected override async Task OnParametersSetAsync()
    {
        IsLoading = true;
        try
        {
            if (IsNew)
            {
                LoadThemeSelection(
                    BuiltInThemeDefaults.Id,
                    BuiltInThemeDefaults.Version,
                    revision: 1);
                await LoadThemeCatalogAsync();
                return;
            }

            var result = await SitesClient.GetByIdAsync(Id!.Value);
            if (result is Result<SiteViewModel, AeroError>.Ok ok)
            {
                LoadSite(ok.Value);
                await LoadThemeCatalogAsync();
            }
            else if (result is Result<SiteViewModel, AeroError>.Failure fail)
            {
                NotificationService.Notify(NotificationSeverity.Error, fail.Error.ToString(), duration: 4000);
                Navigation.NavigateTo("/manager/sites");
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

        /// <summary>
    /// Applies the selected exact theme version independently from the general site form.
    /// </summary>
protected async Task ApplyThemeAsync()
    {
        if (!CanApplyTheme || Id is null)
            return;

        var selectedTheme = ThemeCatalog[SelectedThemeIndex];
        IsApplyingTheme = true;
        ThemeApplyError = null;
        try
        {
            var result = await SitesClient.UpdateThemeAsync(
                Id.Value,
                new UpdateSiteThemeRequest(
                    CurrentThemeRevision,
                    selectedTheme.Id,
                    selectedTheme.Version));

            if (result is Result<SiteThemeSelectionViewModel, AeroError>.Ok ok)
            {
                LoadThemeSelection(ok.Value.ThemeId, ok.Value.ThemeVersion, ok.Value.ThemeRevision);
                NotificationService.Notify(
                    NotificationSeverity.Success,
                    L["Theme applied"],
                    $"{selectedTheme.Name} {selectedTheme.Version}",
                    duration: 4000);
                return;
            }

            if (result is Result<SiteThemeSelectionViewModel, AeroError>.Failure { Error: AeroError.Conflict })
            {
                var reloaded = await ReloadThemeSelectionAsync();
                ThemeApplyError = reloaded
                    ? L["The theme was changed by another editor. The current selection has been reloaded; review it before applying again."]
                    : L["The theme was changed by another editor, but the latest selection could not be loaded. Reload the page before trying again."];
                NotificationService.Notify(
                    NotificationSeverity.Warning,
                    L["Theme changed elsewhere"],
                    ThemeApplyError,
                    duration: 7000);
                return;
            }

            if (result is Result<SiteThemeSelectionViewModel, AeroError>.Failure failure)
            {
                ThemeApplyError = failure.Error.ToString();
                NotificationService.Notify(
                    NotificationSeverity.Error,
                    L["Theme could not be applied"],
                    ThemeApplyError,
                    duration: 6000);
            }
        }
        finally
        {
            IsApplyingTheme = false;
        }
    }

        /// <summary>
    /// Reloads the immutable deployment theme catalog.
    /// </summary>
protected async Task LoadThemeCatalogAsync()
    {
        IsThemeCatalogLoading = true;
        ThemeCatalogError = null;
        try
        {
            var result = await ThemesClient.GetAllAsync();
            if (result is Result<IReadOnlyList<ThemeSummary>, AeroError>.Ok ok)
            {
                ThemeCatalog = ok.Value;
                SelectCurrentTheme();
            }
            else if (result is Result<IReadOnlyList<ThemeSummary>, AeroError>.Failure failure)
            {
                ThemeCatalog = [];
                SelectedThemeIndex = -1;
                ThemeCatalogError = failure.Error.ToString();
            }
        }
        finally
        {
            IsThemeCatalogLoading = false;
        }
    }

        /// <summary>
    /// SaveAsync method.
    /// </summary>
protected async Task SaveAsync()
    {
        if (IsSaving || IsApplyingTheme)
            return;

        if (!Validate())
            return;

        IsSaving = true;
        try
        {
            var cultures = NormalizeSupportedCultures();
            if (IsNew)
            {
                var create = new CreateSiteRequest(
                    Name.Trim(),
                    PrimaryHost.Trim(),
                    Hosts.Count > 0 ? Hosts.ToList() : null,
                    Description,
                    false,
                    DefaultCulture,
                    cultures);

                var result = await SitesClient.CreateAsync(create);
                if (result is Result<SiteViewModel, AeroError>.Ok ok)
                {
                    var profileSaved = await SaveStyleProfileAsync(
                        ok.Value.Id,
                        Math.Max(1, ok.Value.StyleProfile?.Revision ?? 1));
                    if (profileSaved)
                    {
                        NotificationService.Notify(NotificationSeverity.Success, $"Site '{ok.Value.Name}' created");
                        Navigation.NavigateTo("/manager/sites");
                    }
                    else
                    {
                        Navigation.NavigateTo($"/manager/sites/{ok.Value.Id}");
                    }
                }
                else if (result is Result<SiteViewModel, AeroError>.Failure fail)
                {
                    NotificationService.Notify(NotificationSeverity.Error, fail.Error.ToString(), duration: 4000);
                }
            }
            else
            {
                var update = new UpdateSiteRequest(
                    Id!.Value,
                    Name.Trim(),
                    PrimaryHost.Trim(),
                    Hosts.Count > 0 ? Hosts.ToList() : null,
                    Description,
                    false,
                    DefaultCulture,
                    cultures);

                var result = await SitesClient.UpdateAsync(Id.Value, update);
                if (result is Result<SiteViewModel, AeroError>.Ok ok)
                {
                    if (await SaveStyleProfileAsync(Id.Value, _styleProfileRevision))
                    {
                        NotificationService.Notify(NotificationSeverity.Success, $"Site '{ok.Value.Name}' updated");
                        Navigation.NavigateTo("/manager/sites");
                    }
                }
                else if (result is Result<SiteViewModel, AeroError>.Failure fail)
                {
                    NotificationService.Notify(NotificationSeverity.Error, fail.Error.ToString(), duration: 4000);
                }
            }
        }
        finally
        {
            IsSaving = false;
        }
    }

        /// <summary>
    /// AddHost method.
    /// </summary>
protected void AddHost()
    {
        var host = HostDraft.Trim();
        if (string.IsNullOrWhiteSpace(host))
            return;

        if (!Hosts.Contains(host, StringComparer.OrdinalIgnoreCase)
            && !string.Equals(host, PrimaryHost, StringComparison.OrdinalIgnoreCase))
        {
            Hosts.Add(host);
        }

        HostDraft = string.Empty;
    }

        /// <summary>
    /// RemoveHost method.
    /// </summary>
protected void RemoveHost(string host)
        => Hosts.RemoveAll(existing => string.Equals(existing, host, StringComparison.OrdinalIgnoreCase));

        /// <summary>
    /// ToggleLocale method.
    /// </summary>
protected void ToggleLocale(string culture)
    {
        var normalized = AeroLocaleCatalog.NormalizeCultureOrDefault(culture, DefaultCulture);
        if (SupportedCultures.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            RemoveLocale(normalized);
        else
            SupportedCultures.Add(normalized);

        EnsureDefaultCulture();
    }

        /// <summary>
    /// RemoveLocale method.
    /// </summary>
protected void RemoveLocale(string culture)
    {
        if (string.Equals(culture, DefaultCulture, StringComparison.OrdinalIgnoreCase))
            return;

        SupportedCultures.RemoveAll(existing => string.Equals(existing, culture, StringComparison.OrdinalIgnoreCase));
    }

        /// <summary>
    /// LocaleButtonStyle method.
    /// </summary>
protected string LocaleButtonStyle(bool selected)
        => selected
            ? "background: color-mix(in srgb, var(--pe-primary) 12%, transparent); color: var(--pe-primary); border: 1px solid color-mix(in srgb, var(--pe-primary) 40%, var(--pe-border));"
            : "background: var(--pe-bg-secondary); color: var(--pe-text-secondary); border: 1px solid var(--pe-border);";

        /// <summary>
    /// FormatCulture method.
    /// </summary>
protected static string FormatCulture(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
            return "en-US";

        try
        {
            var info = CultureInfo.GetCultureInfo(culture);
            return $"{info.EnglishName} ({info.Name})";
        }
        catch (CultureNotFoundException)
        {
            return culture;
        }
    }

    private void LoadSite(SiteViewModel site)
    {
        Name = site.Name ?? string.Empty;
        PrimaryHost = site.PrimaryHost ?? string.Empty;
        Description = string.Empty;
        Hosts.Clear();
        Hosts.AddRange(site.Hosts
            .Where(host => !string.IsNullOrWhiteSpace(host))
            .Distinct(StringComparer.OrdinalIgnoreCase));

        SupportedCultures.Clear();
        SupportedCultures.AddRange(site.SupportedCultures.Count > 0
            ? site.SupportedCultures.Select(culture => AeroLocaleCatalog.NormalizeCultureOrDefault(culture)).Distinct(StringComparer.OrdinalIgnoreCase)
            : ["en-US"]);

        DefaultCulture = AeroLocaleCatalog.NormalizeCultureOrDefault(site.DefaultCulture, SupportedCultures.FirstOrDefault() ?? "en-US");
        EnsureDefaultCulture();
        LoadStyleProfile(site.StyleProfile);
        LoadThemeSelection(site.ThemeId, site.ThemeVersion, site.ThemeRevision);
    }

    private async Task<bool> ReloadThemeSelectionAsync()
    {
        if (Id is null)
            return false;

        var result = await SitesClient.GetByIdAsync(Id.Value);
        if (result is Result<SiteViewModel, AeroError>.Ok ok)
        {
            LoadThemeSelection(ok.Value.ThemeId, ok.Value.ThemeVersion, ok.Value.ThemeRevision);
            return true;
        }

        if (result is Result<SiteViewModel, AeroError>.Failure failure)
        {
            ThemeApplyError = failure.Error.ToString();
        }

        return false;
    }

    private void LoadThemeSelection(string? themeId, string? themeVersion, long revision)
    {
        CurrentThemeId = string.IsNullOrWhiteSpace(themeId) ? BuiltInThemeDefaults.Id : themeId;
        CurrentThemeVersion = string.IsNullOrWhiteSpace(themeVersion) ? BuiltInThemeDefaults.Version : themeVersion;
        CurrentThemeRevision = Math.Max(1, revision);
        SelectCurrentTheme();
    }

    private void SelectCurrentTheme()
        => SelectedThemeIndex = FindThemeIndex(CurrentThemeId, CurrentThemeVersion);

    private int FindThemeIndex(string themeId, string themeVersion)
    {
        for (var index = 0; index < ThemeCatalog.Count; index++)
        {
            var theme = ThemeCatalog[index];
            if (string.Equals(theme.Id, themeId, StringComparison.Ordinal)
                && string.Equals(theme.Version, themeVersion, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private bool Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            NotificationService.Notify(NotificationSeverity.Warning, "Site name is required");
            return false;
        }

        if (string.IsNullOrWhiteSpace(PrimaryHost))
        {
            NotificationService.Notify(NotificationSeverity.Warning, "Primary host is required");
            return false;
        }

        if (SmallScreenBreakpointRem is < 20 or > 120)
        {
            NotificationService.Notify(
                NotificationSeverity.Warning,
                "The mobile breakpoint must be between 20 and 120 rem.");
            return false;
        }

        EnsureDefaultCulture();
        return true;
    }

    private async Task<bool> SaveStyleProfileAsync(long siteId, long expectedRevision)
    {
        var tokens = StyleTokens
            .Select(static token => new SiteStyleColorTokenViewModel
            {
                Name = token.Name,
                HexValue = token.HexValue
            })
            .Concat(_additionalStyleTokens)
            .ToList();

        var result = await SitesClient.UpdateStyleProfileAsync(
            siteId,
            new UpdateSiteStyleProfileRequest(
                expectedRevision,
                SmallScreenBreakpointRem,
                tokens));

        if (result is Result<SiteStyleProfileViewModel, AeroError>.Ok ok)
        {
            _styleProfileRevision = ok.Value.Revision;
            return true;
        }

        if (result is Result<SiteStyleProfileViewModel, AeroError>.Failure failure)
        {
            NotificationService.Notify(
                NotificationSeverity.Error,
                failure.Error.ToString(),
                duration: 6000);
        }

        return false;
    }

    private void LoadStyleProfile(SiteStyleProfileViewModel? profile)
    {
        _styleProfileRevision = Math.Max(1, profile?.Revision ?? 1);
        SmallScreenBreakpointRem = profile?.SmallScreenBreakpointRem is >= 20 and <= 120
            ? profile.SmallScreenBreakpointRem
            : 48;

        var loadedTokens = (profile?.ColorTokens ?? [])
            .Where(static token => !string.IsNullOrWhiteSpace(token.Name))
            .GroupBy(static token => token.Name, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.Ordinal);

        foreach (var token in StyleTokens)
        {
            if (loadedTokens.TryGetValue(token.Name, out var loaded))
            {
                token.HexValue = loaded.HexValue;
            }
        }

        var curatedNames = StyleTokens
            .Select(static token => token.Name)
            .ToHashSet(StringComparer.Ordinal);
        _additionalStyleTokens = loadedTokens.Values
            .Where(token => !curatedNames.Contains(token.Name))
            .Select(static token => new SiteStyleColorTokenViewModel
            {
                Name = token.Name,
                HexValue = token.HexValue
            })
            .ToList();
    }

    private static List<StyleTokenEditorModel> CreateDefaultStyleTokens() =>
    [
        new("brand-primary", "Primary", "Main actions, links, and brand accents.", "#7c3aed"),
        new("brand-secondary", "Secondary", "Supporting accents and secondary actions.", "#2563eb"),
        new("surface-page", "Page background", "The default public page background.", "#ffffff"),
        new("surface-card", "Card surface", "Cards and elevated content surfaces.", "#f8fafc"),
        new("text-primary", "Primary text", "Headings and normal body copy.", "#172033"),
        new("text-muted", "Muted text", "Descriptions, captions, and supporting copy.", "#64748b")
    ];

    private List<string> NormalizeSupportedCultures()
    {
        EnsureDefaultCulture();
        return SupportedCultures
            .Select(culture => AeroLocaleCatalog.NormalizeCultureOrDefault(culture, DefaultCulture))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void EnsureDefaultCulture()
    {
        DefaultCulture = AeroLocaleCatalog.NormalizeCultureOrDefault(DefaultCulture);
        if (!SupportedCultures.Contains(DefaultCulture, StringComparer.OrdinalIgnoreCase))
            SupportedCultures.Insert(0, DefaultCulture);

        if (SupportedCultures.Count == 0)
            SupportedCultures.Add(DefaultCulture);
    }

    protected sealed class StyleTokenEditorModel(
        string name,
        string label,
        string description,
        string hexValue)
    {
        public string Name { get; } = name;
        public string Label { get; } = label;
        public string Description { get; } = description;
        public string HexValue { get; set; } = hexValue;
    }
}
