using System.Globalization;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Abstractions.Requests;
using Aero.Core;
using Aero.Core.Globalization;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Aero.Cms.Shared.Pages.Manager.Sites;

public partial class SiteEditor : ComponentBase
{
    [Parameter] public long? Id { get; set; }

    [Inject] protected ISitesHttpClient SitesClient { get; set; } = null!;
    [Inject] protected NavigationManager Navigation { get; set; } = null!;

    protected bool IsLoading { get; set; }
    protected bool IsSaving { get; set; }
    protected string Name { get; set; } = string.Empty;
    protected string PrimaryHost { get; set; } = string.Empty;
    protected string Description { get; set; } = string.Empty;
    protected string HostDraft { get; set; } = string.Empty;
    protected string LocaleSearch { get; set; } = string.Empty;
    protected string DefaultCulture { get; set; } = "en-US";
    protected List<string> Hosts { get; } = [];
    protected List<string> SupportedCultures { get; } = ["en-US"];
    protected IReadOnlyList<AeroLocaleOption> LocaleOptions { get; } = AeroLocaleCatalog.GetLocales();

    protected bool IsNew => Id is null or 0;
    protected string PageTitle => IsNew ? "New Site" : $"Edit {Name}";
    protected string SaveButtonText => IsSaving ? "Saving..." : IsNew ? "Create Site" : "Save Site";

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

    protected override async Task OnParametersSetAsync()
    {
        if (IsNew)
            return;

        IsLoading = true;
        try
        {
            var result = await SitesClient.GetByIdAsync(Id!.Value);
            if (result is Result<SiteViewModel, AeroError>.Ok ok)
            {
                LoadSite(ok.Value);
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

    protected async Task SaveAsync()
    {
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
                    NotificationService.Notify(NotificationSeverity.Success, $"Site '{ok.Value.Name}' created");
                    Navigation.NavigateTo("/manager/sites");
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
                    NotificationService.Notify(NotificationSeverity.Success, $"Site '{ok.Value.Name}' updated");
                    Navigation.NavigateTo("/manager/sites");
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

    protected void RemoveHost(string host)
        => Hosts.RemoveAll(existing => string.Equals(existing, host, StringComparison.OrdinalIgnoreCase));

    protected void ToggleLocale(string culture)
    {
        var normalized = AeroLocaleCatalog.NormalizeCultureOrDefault(culture, DefaultCulture);
        if (SupportedCultures.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            RemoveLocale(normalized);
        else
            SupportedCultures.Add(normalized);

        EnsureDefaultCulture();
    }

    protected void RemoveLocale(string culture)
    {
        if (string.Equals(culture, DefaultCulture, StringComparison.OrdinalIgnoreCase))
            return;

        SupportedCultures.RemoveAll(existing => string.Equals(existing, culture, StringComparison.OrdinalIgnoreCase));
    }

    protected string LocaleButtonStyle(bool selected)
        => selected
            ? "background: color-mix(in srgb, var(--pe-primary) 12%, transparent); color: var(--pe-primary); border: 1px solid color-mix(in srgb, var(--pe-primary) 40%, var(--pe-border));"
            : "background: var(--pe-bg-secondary); color: var(--pe-text-secondary); border: 1px solid var(--pe-border);";

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

        EnsureDefaultCulture();
        return true;
    }

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
}
