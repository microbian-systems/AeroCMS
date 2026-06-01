using System.Globalization;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Shared.Services;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;
using Radzen.Blazor;

namespace Aero.Cms.Shared.Pages.Manager.Sites;

public partial class Sites : ComponentBase
{
    [Inject] protected ISitesHttpClient SitesClient { get; set; } = null!;
    [Inject] protected ICurrentSiteAccessor CurrentSiteAccessor { get; set; } = null!;
    [Inject] protected AdminStateContainer AdminState { get; set; } = null!;
    [Inject] protected DialogService DialogService { get; set; } = null!;
    [Inject] protected NavigationManager Navigation { get; set; } = null!;
    [Inject] protected IStringLocalizer<Aero.Cms.Shared.Localization.ManagerResource> L { get; set; } = default!;

    protected RadzenDataGrid<SiteViewModel>? Grid;
    protected IReadOnlyList<SiteViewModel>? SiteRows;
    protected int Count;
    protected bool IsLoading;

    protected async Task LoadData(LoadDataArgs args)
    {
        IsLoading = true;
        try
        {
            var result = await SitesClient.GetAllAsync();
            if (result is Result<IReadOnlyList<SiteViewModel>, AeroError>.Ok ok)
            {
                Count = ok.Value.Count;
                SiteRows = ok.Value.Skip(args.Skip ?? 0).Take(args.Top ?? 10).ToList();
            }
            else if (result is Result<IReadOnlyList<SiteViewModel>, AeroError>.Failure fail)
            {
                NotificationService.Notify(NotificationSeverity.Error, fail.Error.ToString(), duration: 4000);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected async Task DeleteSiteAsync(SiteViewModel site)
    {
        var confirmed = await DialogService.Confirm(
            $"Are you sure you want to delete '{site.Name}'? This action cannot be undone.",
            "Delete Site",
            new ConfirmOptions { OkButtonText = "Delete", CancelButtonText = "Cancel" });
        if (confirmed != true) return;

        var result = await SitesClient.DeleteAsync(site.Id);
        if (result is Result<bool, AeroError>.Ok)
        {
            NotificationService.Notify(NotificationSeverity.Success, $"Site '{site.Name}' deleted");
            if (Grid is not null)
                await Grid.Reload();
        }
        else if (result is Result<bool, AeroError>.Failure fail)
        {
            NotificationService.Notify(NotificationSeverity.Error, fail.Error.ToString(), duration: 4000);
        }
    }

    protected async Task SelectSiteAsync(long siteId, string? siteName)
    {
        await CurrentSiteAccessor.SetCurrentSiteAsync(siteId);
        AdminState.SetSite(siteId, siteName ?? "Site");
        NotificationService.Notify(NotificationSeverity.Success, "Site selected");
        Navigation.NavigateTo(Navigation.Uri, forceLoad: true);
    }

    protected static string FormatCulture(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
            return "en-US";

        try
        {
            var info = CultureInfo.GetCultureInfo(culture);
            return info.Name;
        }
        catch (CultureNotFoundException)
        {
            return culture;
        }
    }
}
